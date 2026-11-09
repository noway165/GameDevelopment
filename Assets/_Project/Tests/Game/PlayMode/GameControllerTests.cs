using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Metropolis.Game.PlayTests
{
    public class GameControllerTests
    {
        [UnityTest]
        public IEnumerator UnwiredController_LogsOnce_AndDisablesItself()
        {
            LogAssert.Expect(LogType.Error, new Regex("GameController"));
            var c = new GameObject().AddComponent<GameController>();
            yield return null;
            yield return null;
            Assert.That(c.enabled, Is.False);
            Assert.That(c.Simulation, Is.Null);
            Object.Destroy(c.gameObject);
        }
    }
}
