using NUnit.Framework;

namespace Metropolis.Core.Tests
{
    public class CatalogTests
    {
        static BuildingDefinition Def(string id, int maintenance = 1) =>
            new BuildingDefinition(id, BuildingKind.Residential, 0, maintenance, 20, 10, 8, 0);

        [Test]
        public void Default_HasSevenValidDefinitions()
        {
            var c = BuildingCatalog.Default();
            Assert.That(c.Variants(BuildingKind.Residential).Count, Is.EqualTo(1));
            Assert.That(c.Variants(BuildingKind.Commercial).Count, Is.EqualTo(2));
            Assert.That(c.Variants(BuildingKind.Industrial).Count, Is.EqualTo(2));
            Assert.That(c.Variants(BuildingKind.Power).Count, Is.EqualTo(1));
            Assert.That(c.Variants(BuildingKind.Water).Count, Is.EqualTo(1));
        }

        [Test]
        public void Duplicate_Id_Throws() => Assert.That(
            () => new BuildingCatalog(new[] { Def("a"), Def("a") }), Throws.ArgumentException);

        [Test]
        public void Empty_Id_Throws() => Assert.That(
            () => new BuildingCatalog(new[] { Def("") }), Throws.ArgumentException);

        [Test]
        public void Negative_Value_Throws() => Assert.That(
            () => new BuildingCatalog(new[] { Def("a", -1) }), Throws.ArgumentException);

        [Test]
        public void TryGet_Unknown_ReturnsFalse() =>
            Assert.That(BuildingCatalog.Default().TryGet("nope", out _), Is.False);
    }
}
