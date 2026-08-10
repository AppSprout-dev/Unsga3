namespace Unsga3.Algorithm;

/// <summary>Ordered collection of individuals (current generation or combined pool).</summary>
public sealed class Population
{
    private readonly List<Individual> _members;

    public Population(int capacity = 0)
    {
        _members = capacity > 0 ? new List<Individual>(capacity) : new List<Individual>();
    }

    public Population(IEnumerable<Individual> individuals)
    {
        _members = new List<Individual>(individuals);
    }

    public int Count => _members.Count;
    public Individual this[int index] => _members[index];
    public IReadOnlyList<Individual> Members => _members;

    public void Add(Individual individual) => _members.Add(individual);
    public void AddRange(IEnumerable<Individual> individuals) => _members.AddRange(individuals);
    public void Clear() => _members.Clear();

    public Population Clone()
    {
        var p = new Population(_members.Count);
        foreach (var ind in _members)
            p.Add(ind.Clone());
        return p;
    }

    public List<Individual> ToList() => new(_members);
}
