namespace Sigmap.Infrastructure.Simulation;

/// <summary>Port of the frontend `mulberry32` PRNG so seeded data matches.</summary>
public sealed class SimRng
{
    private uint _state;

    public SimRng(uint seed) => _state = seed;

    public double Next()
    {
        _state = unchecked(_state + 0x6D2B79F5u);
        var a = unchecked((int)_state);
        int t = Imul(a ^ (int)((uint)a >> 15), 1 | a);
        var oldT = t;
        t = unchecked((oldT + Imul(oldT ^ (int)((uint)oldT >> 7), 61 | oldT)) ^ oldT);
        return ((uint)(t ^ (int)((uint)t >> 14))) / 4294967296.0;
    }

    private static int Imul(int a, int b) => unchecked((int)((uint)a * (uint)b));

    public int Int(int min, int max) => (int)System.Math.Floor(Next() * (max - min + 1)) + min;

    public double Float(double min, double max) => Next() * (max - min) + min;

    public T Pick<T>(IReadOnlyList<T> arr) => arr[(int)System.Math.Floor(Next() * arr.Count)];

    public T WeightedPick<T>(IReadOnlyList<(T Value, int Weight)> entries)
    {
        var total = entries.Sum(e => e.Weight);
        var roll = Next() * total;
        foreach (var (value, weight) in entries)
        {
            roll -= weight;
            if (roll <= 0) return value;
        }
        return entries[^1].Value;
    }
}
