using Metropolis.GameEditor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Metropolis.Game.Tests
{
    public class WorldBuilderTests
    {
        [Test]
        public void Build_LeavesOpenScene_WhenUserKeepsUnsavedWork()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            new GameObject("unsaved work");

            WorldBuilder.BuildUnlessCancelled(() => false);

            Assert.That(GameObject.Find("unsaved work"), Is.Not.Null);
        }
    }
}
