using NUnit.Framework;
using UnityEngine;

namespace Metropolis.Game.Tests
{
    public class CameraRigTests
    {
        [Test]
        public void Clamp_KeepsCameraOverMap_AndWithinZoom() =>
            Assert.That(CameraRig.Clamp(new Vector3(-5, 999, 900), 800, 30, 300), Is.EqualTo(new Vector3(0, 300, 800)));

        [Test]
        public void Clamp_LeavesValidPositionAlone() =>
            Assert.That(CameraRig.Clamp(new Vector3(100, 50, 200), 800, 30, 300), Is.EqualTo(new Vector3(100, 50, 200)));
    }
}
