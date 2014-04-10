﻿using PICSimulator.Helper;
using PICSimulator.Model.Commands;
using PICSimulator.Model.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace PICSimulator.Model
{
	public sealed class PICController
	{
		public FrequencyCounter Frequency = new FrequencyCounter(); // Only to see the Performance
		public uint EmulatedFrequency = 4000000; // In Hz

		private Thread thread;

		public PICControllerMode Mode { get; private set; } // Set to true while running - false when program ended (NOT WHEN PAUSED)
		public PICControllerSpeed SimulationSpeed;
		private bool[] breakpoints;

		private PICCommand[] CommandList;

		public ConcurrentQueue<PICEvent> Incoming_Events = new ConcurrentQueue<PICEvent>();

		private PICMemory Memory;
		private uint register_W = 0x00;
		private CircularStack CallStack = new CircularStack();

		private uint Cycles = 0; // Passed Controller Cycles

		private PICWatchDogTimer WatchDog;
		private PICTimer Tmr0;
		private PICInterruptLogic Interrupt;

		public PICController(PICCommand[] cmds, PICControllerSpeed s)
		{
			Tmr0 = new PICTimer();
			WatchDog = new PICWatchDogTimer();
			Interrupt = new PICInterruptLogic(this);
			Memory = new PICMemory(Tmr0, Interrupt);

			Mode = PICControllerMode.WAITING;
			SimulationSpeed = s;

			CommandList = cmds;
			breakpoints = new bool[cmds.Length];
		}

		#region Running

		private void run()
		{
			HardReset();

			while (Mode != PICControllerMode.FINISHED)
			{
				uint currCmdCycleCount = 0;

				//################
				//#     MISC     #
				//################

				Frequency.Inc();

				if (GetPC() >= CommandList.Length) // PC > Commandcount
				{
					Mode = PICControllerMode.FINISHED;
					continue;
				}

				HandleIncomingEvents();

				//################
				//#  DEBUGGING   #
				//################

				if (Mode == PICControllerMode.FINISHED)
				{
					continue;
				}

				if (Mode == PICControllerMode.PAUSED)
				{
					Thread.Sleep(0); // Release Control

					continue;
				}

				if (Mode == PICControllerMode.CONTINUE)
				{
					Mode = PICControllerMode.RUNNING;
				}
				else if (Mode == PICControllerMode.SKIPONE)
				{
					Mode = PICControllerMode.PAUSED;
				}
				else
				{
					if (breakpoints[GetPC()])
					{
						Mode = PICControllerMode.PAUSED;
						continue; // Continue so the current Cmd is NOT executed
					}
				}

				//################
				//#    FETCH     #
				//################

				PICCommand cmd = CommandList[GetPC()];

				//################
				//# INCREMENT PC #
				//################

				UnreleasedSleep((int)SimulationSpeed);

				SetPC_13Bit(GetPC() + 1);

				//################
				//#   EXECUTE    #
				//################

				cmd.Execute(this);
				currCmdCycleCount = cmd.GetCycleCount(this);

				Cycles += currCmdCycleCount;

				//################
				//#   AFTERMATH  #
				//################

				Interrupt.Update();
				Tmr0.Update(this);
				WatchDog.Update(this, currCmdCycleCount);
			}

			Mode = PICControllerMode.WAITING;
		}

		private void UnreleasedSleep(int s)
		{
			if (s > 0)
				Thread.Sleep(s);
		}

		private void HandleIncomingEvents()
		{
			PICEvent e;
			while (Incoming_Events.TryDequeue(out e))
			{
				HandleEvent(e);
			}
		}

		private void HandleEvent(PICEvent e)
		{
			Debug.WriteLine("[EVENT::FROM_VIEW] " + e);

			if (e is BreakPointChangedEvent)
			{
				BreakPointChangedEvent ce = e as BreakPointChangedEvent;

				breakpoints[ce.Position] = ce.Value;
			}
			else if (e is ChangePICModeEvent)
			{
				ChangePICModeEvent ce = e as ChangePICModeEvent;

				Mode = ce.Value;
			}
			else if (e is ManuallyRegisterChangedEvent)
			{
				ManuallyRegisterChangedEvent ce = e as ManuallyRegisterChangedEvent;

				SetUnbankedRegister(ce.Position, ce.Value);
			}
			else
			{
				throw new ArgumentException(e.ToString());
			}
		}

		public uint GetBankedRegister(uint p)
		{
			return Memory.GetBankedRegister(p);
		}

		public void SetBankedRegister(uint p, uint n)
		{
			Memory.SetBankedRegister(p, n);
		}

		public void SetBankedRegisterBit(uint p, uint bitpos, bool newVal)
		{
			Memory.SetBankedRegisterBit(p, bitpos, newVal);
		}

		public bool GetBankedRegisterBit(uint p, uint bitpos)
		{
			return Memory.GetBankedRegisterBit(p, bitpos);
		}

		public uint GetUnbankedRegister(uint p)
		{
			return Memory.GetRegister(p);
		}

		public void SetUnbankedRegister(uint p, uint n)
		{
			Memory.SetRegister(p, n);
		}

		public void SetUnbankedRegisterBit(uint p, uint bitpos, bool newVal)
		{
			Memory.SetRegisterBit(p, bitpos, newVal);
		}

		public bool GetUnbankedRegisterBit(uint p, uint bitpos)
		{
			return Memory.GetRegisterBit(p, bitpos);
		}

		public void SetWRegister(uint n, bool forceEvent = false)
		{
			n %= 0x100; // Just 4 Safety

			if (register_W != n || forceEvent)
			{
				register_W = n;
			}
		}

		private void HardReset()
		{
			Cycles = 0;
			Memory.HardResetRegister();

			ResetStack();
			Interrupt.Reset();
			WatchDog.Reset();

			SetPC_13Bit(0);
		}

		public void SoftReset()
		{
			Memory.SoftResetRegister();

			ResetStack();
			Interrupt.Reset();

			SetPC_13Bit(0);
		}

		public uint GetWRegister()
		{
			return register_W;
		}

		private void ResetStack()
		{
			CallStack = new CircularStack();
		}

		public uint GetPC()
		{
			return Memory.GetPC();
		}

		public void SetPC_13Bit(uint value)
		{
			Memory.SetPC(value);
		}

		public void SetPC_11Bit(uint value)
		{
			Memory.SetPC_11Bit(value);
		}

		public void PushCallStack(uint v)
		{
			CallStack.Push(v);
		}

		public uint PopCallStack()
		{
			return CallStack.Pop();
		}

		public void DoInterrupt(PICInterruptType Type)
		{
			Interrupt.AddInterrupt(Type);
		}

		#endregion

		#region Control

		public void Start()
		{
			thread = new Thread(new ThreadStart(run));

			Mode = PICControllerMode.RUNNING;

			thread.Start();
		}

		public void StartPaused()
		{
			thread = new Thread(new ThreadStart(run));

			Mode = PICControllerMode.PAUSED;

			thread.Start();
		}

		public void Stop()
		{
			Incoming_Events.Enqueue(new ChangePICModeEvent() { Value = PICControllerMode.FINISHED });
		}

		public void Continue()
		{
			Incoming_Events.Enqueue(new ChangePICModeEvent() { Value = PICControllerMode.CONTINUE });
		}

		public void Step()
		{
			Incoming_Events.Enqueue(new ChangePICModeEvent() { Value = PICControllerMode.SKIPONE });
		}

		public void Pause()
		{
			Incoming_Events.Enqueue(new ChangePICModeEvent() { Value = PICControllerMode.PAUSED });
		}

		#endregion

		#region Helper

		public double GetWatchDogPerc()
		{
			return WatchDog.GetPerc();
		}

		public long GetSCLineForPC(uint pc)
		{
			return pc < CommandList.Length ? CommandList[pc].SourceCodeLine : -1L;
		}

		public long GetPCLineForSCLine(int sc)
		{
			return (CommandList.Count(p => p.SourceCodeLine == sc) == 1) ? CommandList.Single(p => p.SourceCodeLine == sc).Position : -1L;
		}

		public string GetSourceCodeForPC(uint pc)
		{
			return pc < CommandList.Length ? CommandList[pc].SourceCodeText : "";
		}

		public void RaiseCompleteEventResetChain()
		{
			for (uint i = 0; i < 0x100; i++)
			{
				SetUnbankedRegister(i, GetUnbankedRegister(i));
			}

			SetWRegister(GetWRegister());
		}

		public uint GetRunTime() // in us
		{
			return (uint)(Cycles / (EmulatedFrequency / 1000000.0));
		}

		public Stack<uint> GetThreadSafeCallStack()
		{
			return CallStack.getAsNativeStack();
		}

		public bool IsWatchDogEnabled()
		{
			return WatchDog.Enabled;
		}

		public void SetWatchDogEnabled(bool e)
		{
			WatchDog.Enabled = e;
		}

		#endregion

	}
}
