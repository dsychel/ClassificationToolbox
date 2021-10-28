using System;

namespace ClassificationToolboxWPF
{
    public class StatusChangedArg : EventArgs
    {
        public readonly string Status;

        public StatusChangedArg(string status)
        {
            Status = status;
        }
    }

    public class ValueChangedEventArg : EventArgs
    {
        public readonly object PreviousValue;
        public readonly object CurrentValue;

        public ValueChangedEventArg(object previousValue, object currentValue)
        {
            PreviousValue = previousValue;
            CurrentValue = currentValue;
        }
    }

    public class StartedEventsArg : EventArgs
    {
        public readonly string Status;
        public readonly string LogMessage;
        public readonly DateTime EventDate;
        public readonly int MaxProgress;
        public readonly bool ReportProgress;

        public StartedEventsArg(string status, string logMessage, DateTime eventDate, int maxProgress, bool reportProgress = false)
        {
            Status = status;
            LogMessage = logMessage;
            EventDate = eventDate;
            MaxProgress = maxProgress;
            ReportProgress = reportProgress;
        }
    }

    public class ProgressingEventsArg : EventArgs
    {
        public readonly int Progress;
        public readonly int MaxProgress;
        public readonly string LogMessage;

        public ProgressingEventsArg(int progress, int maxProgress = -1, string logMessage = null)
        {
            Progress = progress;
            MaxProgress = maxProgress;
            LogMessage = logMessage;
        }
    }

    public class CompletionEventsArg : EventArgs
    {
        public readonly string Status;
        public readonly string LogMessage;
        public readonly string Error;
        public readonly DateTime EventDate;
        public readonly bool Shutdown;

        public CompletionEventsArg(string status, string logMessage, string error, DateTime eventDate, bool shutdown = false)
        {
            Status = status;
            LogMessage = logMessage;
            Error = error;
            EventDate = eventDate;
            Shutdown = shutdown;
        }
    }
}
