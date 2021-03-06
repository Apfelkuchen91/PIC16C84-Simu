﻿
namespace PICSimulator.Model.Commands
{
	/// <summary>
	/// The contents of register 'f' are comple-
	/// mented. If 'd' is 0 the result is stored in
	/// W. If 'd' is 1 the result is stored back in
	/// register 'f'.
	/// </summary>

	class PICCommand_COMF : PICCommand
	{
		public const string COMMANDCODE = "00 1001 dfff ffff";

		public readonly bool Target;
		public readonly uint Register;

		public PICCommand_COMF(string sct, uint scl, uint pos, uint cmd)
			: base(sct, scl, pos, cmd)
		{
			Target = Parameter.GetBoolParam('d').Value;
			Register = Parameter.GetParam('f').Value;
		}

		public override void Execute(PICController controller)
		{
			uint Result = ~controller.GetBankedRegister(Register);

			controller.SetUnbankedRegisterBit(PICMemory.ADDR_STATUS, PICMemory.STATUS_BIT_Z, Result == 0);

			if (Target)
				controller.SetBankedRegister(Register, Result);
			else
				controller.SetWRegister(Result);
		}

		public override string GetCommandCodeFormat()
		{
			return COMMANDCODE;
		}

		public override uint GetCycleCount(PICController controller)
		{
			return 1;
		}
	}
}
