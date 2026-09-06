using System.Numerics;

namespace ClickToMove.Movement;

internal interface IMovementEngine
{
    string Name { get; }

    bool IsAvailable();

    string UnavailableReason();

    bool Start(Vector3 destination, bool fly);

    void Stop();

    bool IsRunning();

    void Update();
}
