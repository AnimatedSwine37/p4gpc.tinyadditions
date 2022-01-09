using p4gpc.tinyadditions.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X86;
using Reloaded.Memory.Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;

namespace p4gpc.tinyadditions.Additions
{
    internal class LevelUp : Addition
    {
        private LvlUpParty _lvlUpParty;
        public LevelUp(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks) : base(utils, baseAddress, configuration, memory, hooks)
        {
            long lvlUpAddress = _utils.SigScan("53 56 57 8B F9 B9 ?? ?? ?? ?? E8 ?? ?? ?? ?? B9 4C 00 00 00", "level up party member");
            // Create a wrapper for the native level up party member function that can be called
            _lvlUpParty = _hooks.CreateWrapper<LvlUpParty>(lvlUpAddress, out IntPtr lvlUpThingAddress);
        }

        public void LvlUp(int partyMember)
        {
            _lvlUpParty(partyMember);
        }

        public override void Resume()
        {
            throw new NotImplementedException();
        }

        public override void Suspend()
        {
            throw new NotImplementedException();
        }

        // Delegates for functions
        [Function(Register.eax, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool LvlUpParty(int partyMember);

    }
}
