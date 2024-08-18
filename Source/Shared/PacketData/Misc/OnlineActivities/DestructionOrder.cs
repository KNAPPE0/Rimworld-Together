using System;

namespace Shared
{
    [Serializable]
    public class DestructionOrder
    {
        // Index of the target to destroy
        public int IndexToDestroy { get; set; } = 0; // Default value is 0, can be set to the target's index
    }
}