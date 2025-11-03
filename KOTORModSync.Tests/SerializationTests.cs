// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using KOTORModSync.Core.Utility;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class SerializationTests
    {
        [Test]
        public void TestSerializeString()
        {
            const string str = "Hello, world!";
            object? serialized = Serializer.SerializeObject(str);

            Assert.That(serialized, Is.EqualTo(str));
        }

        [Test]
        public void TestSerializeInt()
        {
            int thisInt = new Random().Next(minValue: 1, maxValue: 65535);
            object? serialized = Serializer.SerializeObject(thisInt);

            Assert.That(serialized, Is.EqualTo(thisInt.ToString()));
        }

        [Test]
        public void TestSerializeGuid()
        {
            var guid = Guid.NewGuid();
            object? serialized = Serializer.SerializeObject(guid);

            if (serialized is null)
            {
                throw new NullReferenceException(nameof(serialized));
            }

            Assert.That(
                serialized,
                Is.EqualTo(guid.ToString()),
                "Serialized value should be equal to the string representation"
                + $" of the Guid,{Environment.NewLine}but was {serialized.GetType()}"
            );
        }

        [Test]
        public void TestSerializeListOfGuid()
        {
            List<Guid> list =
            [
                Guid.NewGuid(), Guid.NewGuid(),
            ];
            object? serialized = Serializer.SerializeObject(list);

            Assert.Multiple(() =>
            {
                Assert.That(serialized, Is.InstanceOf<IEnumerable<object>>());
                Assert.That((IEnumerable<object>)serialized, Is.All.InstanceOf<string>());
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
                        new(instance1),
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
                        new(instance2), new(instance2),
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
            if (serialized is not Dictionary<string, object> serializedDict)
            {
                if (serialized is not List<object> serializedList)
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
        public MyNestedClass? NestedInstance { get; set; }
        public Dictionary<Guid, List<MyNestedClass>>? GuidNestedClassDict { get; set; }
    }

    public class MyNestedClass
    {
        public MyNestedClass(MyClass parentInstance) => ParentInstance = parentInstance;

        public MyClass ParentInstance { get; set; }
    }
}
