using Metropolis.Core;
using NUnit.Framework;
using UnityEngine;

namespace Metropolis.Game.Tests
{
    public class GridMapperTests
    {
        static GridMapper Map() => new GridMapper(8f, 100, 100);

        [TestCase(0f, 0f, 0, 0)]
        [TestCase(7.99f, 7.99f, 0, 0)]
        [TestCase(8f, 0f, 1, 0)]
        [TestCase(799.9f, 799.9f, 99, 99)]
        public void TryGetCell_Inside(float x, float z, int cx, int cy)
        {
            Assert.That(Map().TryGetCell(new Vector3(x, 0, z), out var cell), Is.True);
            Assert.That(cell, Is.EqualTo(new Cell(cx, cy)));
        }

        [TestCase(-0.01f, 5f)]
        [TestCase(800f, 5f)]
        [TestCase(5f, 800f)]
        [TestCase(5f, -1f)]
        public void TryGetCell_Outside(float x, float z) =>
            Assert.That(Map().TryGetCell(new Vector3(x, 0, z), out _), Is.False);

        [Test]
        public void CellCenter_IsMiddleOfCell() =>
            Assert.That(Map().CellCenter(new Cell(3, 7)), Is.EqualTo(new Vector3(28f, 0f, 60f)));
    }
}
