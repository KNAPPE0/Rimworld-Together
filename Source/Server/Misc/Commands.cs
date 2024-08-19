namespace GameServer
{
    public class ChatCommand
    {
        // Private fields with backing properties
        private string _prefix;
        private string _description;
        private int _parameters;
        private Action _commandAction;

        // Public properties to encapsulate fields
        public string Prefix
        {
            get => _prefix;
            private set => _prefix = value ?? throw new ArgumentNullException(nameof(value), "Prefix cannot be null");
        }

        public string Description
        {
            get => _description;
            private set => _description = value ?? throw new ArgumentNullException(nameof(value), "Description cannot be null");
        }

        public int Parameters
        {
            get => _parameters;
            private set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Parameters count cannot be negative");
                _parameters = value;
            }
        }

        public Action CommandAction
        {
            get => _commandAction;
            private set => _commandAction = value ?? throw new ArgumentNullException(nameof(value), "Command action cannot be null");
        }

        // Constructor with validation
        public ChatCommand(string prefix, int parameters, string description, Action commandAction)
        {
            Prefix = prefix;
            Parameters = parameters;
            Description = description;
            CommandAction = commandAction;
        }
    }

    public class ServerCommand
    {
        // Private fields with backing properties
        private string _prefix;
        private string _description;
        private int _parameters;
        private Action _commandAction;

        // Public properties to encapsulate fields
        public string Prefix
        {
            get => _prefix;
            private set => _prefix = value ?? throw new ArgumentNullException(nameof(value), "Prefix cannot be null");
        }

        public string Description
        {
            get => _description;
            private set => _description = value ?? throw new ArgumentNullException(nameof(value), "Description cannot be null");
        }

        public int Parameters
        {
            get => _parameters;
            private set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Parameters count cannot be negative");
                _parameters = value;
            }
        }

        public Action CommandAction
        {
            get => _commandAction;
            private set => _commandAction = value ?? throw new ArgumentNullException(nameof(value), "Command action cannot be null");
        }

        // Constructor with validation
        public ServerCommand(string prefix, int parameters, string description, Action commandAction)
        {
            Prefix = prefix;
            Parameters = parameters;
            Description = description;
            CommandAction = commandAction;
        }
    }
}