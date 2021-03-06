﻿
using PICSimulator.Helper;
namespace PICSimulator.Model
{
	class PICTimer
	{
		private uint prescale_cntr = 0;

		private bool prev_RA4 = false;

		public PICTimer()
		{

		}

		public void Update(PICController controller, uint cycleCount)
		{
			bool tmr_mode = controller.GetUnbankedRegisterBit(PICMemory.ADDR_OPTION, PICMemory.OPTION_BIT_T0CS);
			bool edge_mode = controller.GetUnbankedRegisterBit(PICMemory.ADDR_OPTION, PICMemory.OPTION_BIT_T0SE);

			if (tmr_mode)
			{
				bool curr_A4 = controller.GetUnbankedRegisterBit(PICMemory.ADDR_PORT_A, 4);

				if (edge_mode)
				{
					if (prev_RA4 && !curr_A4)
					{
						Inc(controller, 1);
					}
				}
				else
				{
					if (!prev_RA4 && curr_A4)
					{
						Inc(controller, 1);
					}
				}
			}
			else
			{
				Inc(controller, cycleCount);
			}

			prev_RA4 = controller.GetUnbankedRegisterBit(PICMemory.ADDR_PORT_A, 4);
		}

		private void Inc(PICController controller, uint cycleCount)
		{
			uint current = controller.GetUnbankedRegister(PICMemory.ADDR_TMR0);
			uint scale = GetPreScale(controller);

			prescale_cntr += cycleCount;

			while (prescale_cntr >= scale)
			{
				prescale_cntr -= scale;

				uint Result = current + 1;
				if (Result > 0xFF)
				{
					controller.DoInterrupt(PICInterruptType.PIT_TIMER);
				}

				Result %= 0x100;

				uint tmp_psc = prescale_cntr;
				controller.SetUnbankedRegister(PICMemory.ADDR_TMR0, Result);
				prescale_cntr = tmp_psc;
			}
		}

		private uint GetPreScale(PICController controller)
		{
			bool prescale_mode = controller.GetUnbankedRegisterBit(PICMemory.ADDR_OPTION, PICMemory.OPTION_BIT_PSA);

			uint scale = 0;
			scale += controller.GetUnbankedRegisterBit(PICMemory.ADDR_OPTION, PICMemory.OPTION_BIT_PS2) ? 1U : 0U;
			scale *= 2;
			scale += controller.GetUnbankedRegisterBit(PICMemory.ADDR_OPTION, PICMemory.OPTION_BIT_PS1) ? 1U : 0U;
			scale *= 2;
			scale += controller.GetUnbankedRegisterBit(PICMemory.ADDR_OPTION, PICMemory.OPTION_BIT_PS0) ? 1U : 0U;

			return prescale_mode ? 1 : (BinaryHelper.SHL(2, scale));
		}

		private uint UIntPower(uint x, uint power)
		{
			if (power == 0)
				return 1;
			if (power == 1)
				return x;
			// ----------------------
			int n = 15;
			while ((power <<= 1) >= 0)
				n--;

			uint tmp = x;
			while (--n > 0)
				tmp = tmp * tmp *
					 (((power <<= 1) < 0) ? x : 1);
			return tmp;
		}

		public void clearPrescaler()
		{
			prescale_cntr = 0;
		}
	}
}
