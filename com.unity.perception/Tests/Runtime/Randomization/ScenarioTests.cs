﻿using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Perception.Randomization.Configuration;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Samplers;
using UnityEngine.Perception.Randomization.Scenarios;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RandomizationTests
{
    [TestFixture]
    public class ScenarioTests
    {
        GameObject m_TestObject;
        FixedLengthScenario m_Scenario;

        [SetUp]
        public void Setup()
        {
            m_TestObject = new GameObject();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_TestObject);
        }

        // TODO: update this function once the perception camera doesn't skip the first frame
        IEnumerator CreateNewScenario()
        {
            m_Scenario = m_TestObject.AddComponent<FixedLengthScenario>();
            m_Scenario.quitOnComplete = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator OverwritesConstantsOnSerialization()
        {
            yield return CreateNewScenario();
            m_Scenario.serializedConstantsFileName = "perception_serialization_test";

            var constants = new FixedLengthScenario.Constants
            {
                framesPerIteration = 2,
                startingIteration = 2,
                totalIterations = 2
            };

            var changedConstants = new FixedLengthScenario.Constants
            {
                framesPerIteration = 0,
                startingIteration = 0,
                totalIterations = 0
            };

            // Serialize some values
            m_Scenario.constants = constants;
            m_Scenario.Serialize();

            // Change the values
            m_Scenario.constants = changedConstants;
            m_Scenario.Deserialize();

            // Check if the values reverted correctly
            Assert.AreEqual(m_Scenario.constants.framesPerIteration, constants.framesPerIteration);
            Assert.AreEqual(m_Scenario.constants.startingIteration, constants.startingIteration);
            Assert.AreEqual(m_Scenario.constants.totalIterations, constants.totalIterations);

            // Clean up serialized constants
            File.Delete(m_Scenario.serializedConstantsFilePath);

            yield return null;
        }

        [UnityTest]
        public IEnumerator IterationsCanLastMultipleFrames()
        {
            yield return CreateNewScenario();
            const int testIterationFrameCount = 5;
            m_Scenario.constants.framesPerIteration = testIterationFrameCount;

            for (var i = 0; i < testIterationFrameCount; i++)
            {
                Assert.AreEqual(0, m_Scenario.currentIteration);
                yield return null;
            }
            Assert.AreEqual(1, m_Scenario.currentIteration);
        }

        [UnityTest]
        public IEnumerator FinishesWhenIsScenarioCompleteIsTrue()
        {
            yield return CreateNewScenario();
            const int testIterationTotal = 5;
            m_Scenario.constants.framesPerIteration = 1;
            m_Scenario.constants.totalIterations = testIterationTotal;

            for (var i = 0; i < testIterationTotal; i++)
            {
                Assert.False(m_Scenario.isScenarioComplete);
                yield return null;
            }
            Assert.True(m_Scenario.isScenarioComplete);
        }

        [UnityTest]
        public IEnumerator AppliesParametersEveryFrame()
        {
            yield return CreateNewScenario();
            m_Scenario.constants.framesPerIteration = 5;
            m_Scenario.constants.totalIterations = 1;

            var config = m_TestObject.AddComponent<ParameterConfiguration>();
            var parameter = config.AddParameter<Vector3Parameter>();
            parameter.x = new UniformSampler(1, 2);
            parameter.y = new UniformSampler(1, 2);
            parameter.z = new UniformSampler(1, 2);
            parameter.target.AssignNewTarget(
                m_TestObject, m_TestObject.transform, "position", ParameterApplicationFrequency.EveryFrame);

            var initialPosition = new Vector3();
            m_TestObject.transform.position = initialPosition;

            yield return null;
            Assert.AreNotEqual(initialPosition, m_TestObject.transform.position);
        }

        [UnityTest]
        public IEnumerator AppliesParametersEveryIteration()
        {
            var config = m_TestObject.AddComponent<ParameterConfiguration>();
            var parameter = config.AddParameter<Vector3Parameter>();
            parameter.x = new UniformSampler(1, 2);
            parameter.y = new UniformSampler(1, 2);
            parameter.z = new UniformSampler(1, 2);

            var transform = m_TestObject.transform;
            transform.position = new Vector3();
            parameter.target.AssignNewTarget(
                m_TestObject, transform, "position", ParameterApplicationFrequency.OnIterationSetup);


            yield return CreateNewScenario();
            m_Scenario.constants.framesPerIteration = 2;
            m_Scenario.constants.totalIterations = 2;

            yield return new WaitForEndOfFrame();
            var initialPosition = transform.position;
            Assert.AreNotEqual(new Vector3(), initialPosition);

            yield return new WaitForEndOfFrame();
            // ReSharper disable once Unity.InefficientPropertyAccess
            var nextPosition = transform.position;
            Assert.AreEqual(initialPosition, nextPosition);

            yield return new WaitForEndOfFrame();
            // ReSharper disable once Unity.InefficientPropertyAccess
            Assert.AreNotEqual(nextPosition, transform.position);
        }

        [UnityTest]
        public IEnumerator StartNewDatasetSequenceEveryIteration()
        {
            var perceptionCamera = SetupPerceptionCamera();

            yield return CreateNewScenario();
            m_Scenario.constants.framesPerIteration = 2;
            m_Scenario.constants.totalIterations = 2;

            // Skip first frame
            yield return new WaitForEndOfFrame();
            Assert.AreEqual(DatasetCapture.SimulationState.SequenceTime, 0);

            // Second frame, first iteration
            yield return new WaitForEndOfFrame();
            Assert.AreEqual(DatasetCapture.SimulationState.SequenceTime, perceptionCamera.period);

            // Third frame, second iteration, SequenceTime has been reset
            yield return new WaitForEndOfFrame();
            Assert.AreEqual(DatasetCapture.SimulationState.SequenceTime, 0);
        }

        PerceptionCamera SetupPerceptionCamera()
        {
            m_TestObject.SetActive(false);
            var camera = m_TestObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1;

            var perceptionCamera = m_TestObject.AddComponent<PerceptionCamera>();
            perceptionCamera.captureRgbImages = false;

            m_TestObject.SetActive(true);
            return perceptionCamera;
        }
    }
}
