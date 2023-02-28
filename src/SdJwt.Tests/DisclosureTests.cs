using NUnit.Framework;
using SdJwt.Models;

namespace SdJwt.Tests
{
    public class DisclosureTests
    {
        private const string SerialisedDisclosure = "WyJfMjZiYzRMVC1hYzZxMktJNmNCVzVlcyIsImZhbWlseV9uYW1lIiwiTVx1MDBGNmJpdXMiXQ";
        private readonly Disclosure DeserialisedDisclosure = new Disclosure()
            { Salt = "_26bc4LT-ac6q2KI6cBW5es", Name = "family_name", Value = "Möbius" };

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void CanSerializeDisclosure()
        {
            Assert.AreEqual(SerialisedDisclosure, DeserialisedDisclosure.Serialize());
        }

        [Test]
        public void CanDeserializeDisclosure()
        {
            Assert.AreEqual(DeserialisedDisclosure.Salt,  Disclosure.Deserialize(SerialisedDisclosure).Salt);
            Assert.AreEqual(DeserialisedDisclosure.Name,  Disclosure.Deserialize(SerialisedDisclosure).Name);
            Assert.AreEqual(DeserialisedDisclosure.Value,  Disclosure.Deserialize(SerialisedDisclosure).Value);
        }

        [Test]
        public void CanComputeHash()
        {
            const string expectedHash = "X-iQ9cBjlRvwtSc2v6NNJiZkvm98vDKXJhS-5JHUt6k";

            string actualHash = DeserialisedDisclosure.GetDigest();
        
            Assert.AreEqual(expectedHash, actualHash);
        }
    }
}

