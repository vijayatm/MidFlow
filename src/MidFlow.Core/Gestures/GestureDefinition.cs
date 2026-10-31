namespace MidFlow.Core.Gestures;

public enum GestureActionKind { Back = 0, Forward = 1, LaunchApplication = 2, Up = 3, Down = 4 }

public sealed record GestureActionDefinition(GestureActionKind Kind, string? ApplicationPath = null);

public sealed record GestureDefinition(string Id, string Name, bool Enabled, GesturePath Path, GestureActionDefinition Action)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Id) && Id.Length <= 64 &&
        !string.IsNullOrWhiteSpace(Name) && Name.Length <= 48 && Path is not null && Path.IsValid &&
        Action is not null && Enum.IsDefined(Action.Kind) &&
        (Action.Kind != GestureActionKind.LaunchApplication ||
         (!string.IsNullOrWhiteSpace(Action.ApplicationPath) && System.IO.Path.IsPathFullyQualified(Action.ApplicationPath)));
}

public sealed record GestureProfile(bool InheritGlobal, IReadOnlyList<GestureDefinition> Gestures)
{
    public static GestureProfile Inherited { get; } = new(true, []);
}

public static class GestureResolver
{
    public static IReadOnlyList<GestureDefinition> Resolve(string? processName,
        IReadOnlyList<GestureDefinition> global, IReadOnlyDictionary<string, GestureProfile> profiles)
    {
        if (!string.IsNullOrWhiteSpace(processName))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(processName.Trim());
            var match = profiles.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase));
            if (match.Value is { } profile)
            {
                if (!profile.InheritGlobal) return profile.Gestures;
                var merged = global.ToDictionary(g => g.Id, StringComparer.OrdinalIgnoreCase);
                foreach (var local in profile.Gestures) merged[local.Id] = local;
                return merged.Values.ToArray();
            }
        }
        return global;
    }

    public static GestureDefinition? Match(GesturePath input, IReadOnlyList<GestureDefinition> gestures)
    {
        GestureDefinition? best = null;
        var bestDistance = double.PositiveInfinity;
        var secondDistance = double.PositiveInfinity;
        foreach (var gesture in gestures)
        {
            if (!gesture.Enabled || !gesture.IsValid) continue;
            var distance = input.DistanceTo(gesture.Path);
            if (distance < bestDistance) { secondDistance = bestDistance; bestDistance = distance; best = gesture; }
            else if (distance < secondDistance) secondDistance = distance;
        }
        // Require separation from the next closest mapping to avoid surprising actions.
        return best is not null && bestDistance <= 0.22 && secondDistance - bestDistance >= 0.035 ? best : null;
    }
}
