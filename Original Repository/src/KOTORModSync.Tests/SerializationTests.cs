// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;

using KOTORModSync.Core.Utility;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class SerializationTests
    {
        [Test]
        public void TestSerializeString()
        {
            const string str = "Hello, world!";
            object serialized = Serializer.SerializeObject(str);

            Assert.Multiple(() =>
            {
                Assert.That(str, Is.Not.Null.And.Not.Empty, "Input string should not be null or empty");
                Assert.That(serialized, Is.Not.Null, "Serialized object should not be null");
                Assert.That(serialized, Is.InstanceOf<string>(), "Serialized object should be a string");
                Assert.That(serialized, Is.EqualTo(str), "Serialized string should equal original string");
            });
        }

        [Test]
        public void TestSerializeInt()
        {
            int thisInt = new Random().Next(minValue: 1, maxValue: 65535);
            object serialized = Serializer.SerializeObject(thisInt);

            Assert.Multiple(() =>
            {
                Assert.That(thisInt, Is.GreaterThan(0), "Input integer should be greater than 0");
                Assert.That(serialized, Is.Not.Null, "Serialized object should not be null");
                Assert.That(serialized, Is.InstanceOf<string>(), "Serialized integer should be a string");
                Assert.That(serialized, Is.EqualTo(thisInt.ToString()), "Serialized integer should equal string representation");
            });
        }

        [Test]
        public void TestSerializeGuid()
        {
            var guid = Guid.NewGuid();
            object serialized = Serializer.SerializeObject(guid);

            Assert.Multiple(() =>
            {
                Assert.That(guid, Is.Not.EqualTo(Guid.Empty), "Input GUID should not be empty");
                Assert.That(serialized, Is.Not.Null, "Serialized object should not be null");
                Assert.That(serialized, Is.InstanceOf<string>(), "Serialized GUID should be a string");
                Assert.That(serialized, Is.EqualTo(guid.ToString()), "Serialized GUID should equal string representation");
            });
        }

        [Test]
        public void TestSerializeListOfGuid()
        {
            List<Guid> list = new List<Guid>
            {
                Guid.NewGuid(), Guid.NewGuid(),
            };
            object serialized = Serializer.SerializeObject(list);

            Assert.Multiple(() =>
            {
                Assert.That(list, Is.Not.Null, "Input list should not be null");
                Assert.That(list, Has.Count.EqualTo(2), "Input list should contain exactly 2 GUIDs");
                Assert.That(list, Is.All.Not.EqualTo(Guid.Empty), "All GUIDs in list should not be empty");
                Assert.That(serialized, Is.Not.Null, "Serialized object should not be null");
                Assert.That(serialized, Is.InstanceOf<IEnumerable<object>>(), "Serialized list should be enumerable");
                Assert.That((IEnumerable<object>)serialized, Is.Not.Empty, "Serialized enumerable should not be empty");
                Assert.That((IEnumerable<object>)serialized, Is.All.InstanceOf<string>(), "All serialized items should be strings");
            });
        }

        [Test]
        public void TestSerializeObjectRecursionProblems()
        {

            var instance1 = new MyClass();
            instance1.NestedInstance = new MyNestedClass(instance1);
            instance1.GuidNestedClassDict = new Dictionary<Guid, List<MyNestedClass>>
            {
                {
                    Guid.NewGuid(), new List<MyNestedClass>
                    {
                        new MyNestedClass(instance1),
                    }
                },
            };

            var instance2 = new MyClass();
            instance2.NestedInstance = new MyNestedClass(instance2);
            instance2.GuidNestedClassDict = new Dictionary<Guid, List<MyNestedClass>>
            {
                {
                    Guid.NewGuid(), new List<MyNestedClass>
                    {
                        new MyNestedClass(instance2), new MyNestedClass(instance2),
                    }
                },
            };

            Assert.Multiple(
                () =>
                {
                    Assert.That(
                        HasStackOverflow(() => Serializer.SerializeObject(instance1)),
                        Is.False,
                        message: "Serialization should not cause a stack overflow"
                    );
                    Assert.That(
                        HasStackOverflow(
                            () => Serializer.SerializeObject(
                                new List<object>
                                {
                                    instance1, instance2,
                                }
                            )
                        ),
                        Is.False,
                        message: "Serialization should not cause a stack overflow"
                    );
                }
            );
        }

        private const int MaxRecursionDepth = 1000;

        private static bool HasStackOverflow(Action action)
        {
            int recursionDepth = 0;
            bool stackOverflow = false;

            try
            {

                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                {
                    if (args.ExceptionObject is StackOverflowException)
                    {
                        stackOverflow = true;
                    }
                };

                _ = ThreadPool.QueueUserWorkItem(
                    _ =>
                    {
                        try
                        {

                            RecursiveMethod(action, ref recursionDepth);
                        }
                        catch
                        {

                        }
                    }
                );

                Thread.Sleep(TimeSpan.FromSeconds(5));

                if (recursionDepth > MaxRecursionDepth)
                {
                    stackOverflow = true;
                }
            }
            catch
            {

            }

            return stackOverflow;
        }

        private static void RecursiveMethod(Action action, ref int recursionDepth)
        {
            recursionDepth++;

            if (recursionDepth > MaxRecursionDepth)
            {
                throw new StackOverflowException("Recursion depth exceeded the limit.");
            }

            action.Invoke();

            recursionDepth--;
        }

        private static void VerifyUniqueSerialization(object serialized, ISet<object> serializedObjects)
        {
            if (!(serialized is Dictionary<string, object> serializedDict))
            {
                if (!(serialized is List<object> serializedList))
                {
                    return;
                }

                foreach (object serializedItem in serializedList)
                {
                    VerifyUniqueSerialization(serializedItem, serializedObjects);
                }

                return;
            }

            foreach (object serializedValue in serializedDict.Values)
            {
                switch (serializedValue)
                {
                    case Dictionary<string, object> nestedDict:
                        VerifyUniqueSerialization(nestedDict, serializedObjects);
                        return;

                    case List<object> serializedList:
                        VerifyUniqueSerialization(serializedList, serializedObjects);
                        return;
                }

                if (!serializedObjects.Add(serializedValue))
                {
                    Assert.Fail($"Duplicate object found during serialization: {serializedValue.GetType().Name}");
                }
            }
        }

    }

    public class MyClass
    {
        public MyNestedClass NestedInstance { get; set; }
        public Dictionary<Guid, List<MyNestedClass>> GuidNestedClassDict { get; set; }
    }

    public class MyNestedClass
    {
        public MyNestedClass(MyClass parentInstance) => ParentInstance = parentInstance;

        public MyClass ParentInstance { get; set; }
    }
}
