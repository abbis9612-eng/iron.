using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IronHorizon.Tests
{
    public class PlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator PlayerLoop_AdvancesFrames()
        {
            int start = Time.frameCount;
            yield return null;
            yield return null;
            Assert.Greater(Time.frameCount, start);
        }
    }
}
