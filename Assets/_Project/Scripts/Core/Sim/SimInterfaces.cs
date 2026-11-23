namespace IronHorizon.Core.Sim
{
    /// <summary>A simulation system stepped once per tick, in registration order.</summary>
    public interface ISimSystem
    {
        void Step(int tick);
    }

    /// <summary>Applies one command type to the simulation state.</summary>
    public interface ICommandHandler
    {
        void Execute(in SimCommand command);
    }

    /// <summary>Notified after a tick has fully completed (commands, then systems).</summary>
    public interface ITickListener
    {
        void OnTickCompleted(int tick);
    }
}
