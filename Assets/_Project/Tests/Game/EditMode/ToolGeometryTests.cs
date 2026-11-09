using Metropolis.Core;
using NUnit.Framework;

namespace Metropolis.Game.Tests
{
    public class ToolGeometryTests
    {
        static Cell C(int x, int y) => new Cell(x, y);

        [Test]
        public void Snap_KeepsLongerAxisX() => Assert.That(ToolGeometry.SnapToAxis(C(5, 5), C(9, 7)), Is.EqualTo(C(9, 5)));

        [Test]
        public void Snap_KeepsLongerAxisY() => Assert.That(ToolGeometry.SnapToAxis(C(5, 5), C(6, 9)), Is.EqualTo(C(5, 9)));

        [Test]
        public void Snap_TieStaysOnX() => Assert.That(ToolGeometry.SnapToAxis(C(5, 5), C(8, 8)), Is.EqualTo(C(8, 5)));

        [Test]
        public void Rectangle_ListsAscending_FromAnyCorner() => Assert.That(
            ToolGeometry.Rectangle(C(2, 2), C(1, 3)), Is.EqualTo(new[] { C(1, 2), C(2, 2), C(1, 3), C(2, 3) }));

        [Test]
        public void Rectangle_SingleCell() => Assert.That(ToolGeometry.Rectangle(C(4, 4), C(4, 4)).Count, Is.EqualTo(1));
    }
}
