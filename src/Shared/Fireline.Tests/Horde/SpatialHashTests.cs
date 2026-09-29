using System;
using System.Collections.Generic;
using System.Numerics;
using Fireline.Shared.Horde;
using NUnit.Framework;

namespace Fireline.Tests.Horde;

public class SpatialHashTests
{
    private SpatialHash _hash;
    private List<int> _results;

    [SetUp]
    public void SetUp()
    {
        _hash = new SpatialHash(1f);
        _results = new List<int>();
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    public void Constructor_NonPositiveCellSize_Throws(float cellSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialHash(cellSize));
    }

    [Test]
    public void Query_FindsItemInSameCell()
    {
        _hash.Insert(7, new Vector2(0.2f, 0.3f));

        _hash.QueryNeighbors(new Vector2(0.5f, 0.5f), _results);

        Assert.That(_results, Is.EquivalentTo(new[] { 7 }));
    }

    [Test]
    public void Query_FindsItemsInAllEightAdjacentCells()
    {
        int id = 0;
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                _hash.Insert(id++, new Vector2(x + 0.5f, y + 0.5f));

        _hash.QueryNeighbors(new Vector2(0.5f, 0.5f), _results);

        Assert.That(_results, Has.Count.EqualTo(9));
    }

    [Test]
    public void Query_IgnoresItemsTwoCellsAway()
    {
        _hash.Insert(1, new Vector2(2.5f, 0.5f));

        _hash.QueryNeighbors(new Vector2(0.5f, 0.5f), _results);

        Assert.That(_results, Is.Empty);
    }

    [Test]
    public void Query_HandlesNegativeCoordinates()
    {
        _hash.Insert(1, new Vector2(-0.5f, -0.5f));
        _hash.Insert(2, new Vector2(-2.5f, -0.5f));

        _hash.QueryNeighbors(new Vector2(0.5f, 0.5f), _results);

        Assert.That(_results, Is.EquivalentTo(new[] { 1 }));
    }

    [Test]
    public void Clear_RemovesEverything()
    {
        _hash.Insert(1, Vector2.Zero);
        _hash.Insert(2, Vector2.One);

        _hash.Clear();
        _hash.QueryNeighbors(Vector2.Zero, _results);

        Assert.That(_results, Is.Empty);
        Assert.That(_hash.Count, Is.EqualTo(0));
    }

    [Test]
    public void Query_OverwritesPreviousResults()
    {
        _results.Add(99);

        _hash.QueryNeighbors(Vector2.Zero, _results);

        Assert.That(_results, Is.Empty);
    }

    [Test]
    public void Count_TracksInserts()
    {
        _hash.Insert(1, Vector2.Zero);
        _hash.Insert(2, new Vector2(10f, 10f));

        Assert.That(_hash.Count, Is.EqualTo(2));
    }
}
