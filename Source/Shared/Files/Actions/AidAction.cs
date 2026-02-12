using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Files.Actions
{
    public class AidAction : BaseAction
    {
        public bool IsEnabled { get; set; } = true;

        public double Cooldown { get; set; } = -1;
    }
}
