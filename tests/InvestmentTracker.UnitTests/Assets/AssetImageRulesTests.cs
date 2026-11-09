using InvestmentTracker.Application.Assets;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Testing;

namespace InvestmentTracker.UnitTests.Assets
{
    public class AssetImageRulesTests
    {
        [Fact]
        public void ImagesHaveContentVersionsAndRepeatedUploadsAreIdempotent()
        {
            var png = TestPng.Create(); var version = AssetImageRules.Validate(new(png), null)!;
            Assert.Matches("^[a-f0-9]{64}$", version);
            Assert.Equal(version, AssetImageRules.Validate(new(png), version));
            Assert.NotEqual(version, AssetImageRules.Validate(new(TestPng.Create(40), version), version));
        }
        [Fact]
        public void ReplacingOrRemovingAnImageRequiresItsCurrentVersion()
        {
            var version = AssetImageRules.Validate(new(TestPng.Create()), null)!;
            Assert.Throws<ResourceConflictException>(() => AssetImageRules.Validate(new(TestPng.Create(40)), version));
            Assert.Throws<ResourceConflictException>(() => AssetImageRules.Validate(new(null), version));
            Assert.Null(AssetImageRules.Validate(new(null, version), version));
        }
        [Fact]
        public void InvalidFormatsCorruptionAndTrailingContentAreRejected()
        {
            var corrupt = TestPng.Create(); corrupt[45] ^= 1;
            foreach (var data in new[] { Array.Empty<byte>(), System.Text.Encoding.UTF8.GetBytes("<svg onload='alert(1)'/>"), corrupt,
                TestPng.Create(corruptCompression: true), TestPng.Create().Concat(new byte[] { 1 }).ToArray(), new byte[AssetImageRules.MaximumBytes + 1] })
                Assert.Throws<InputValidationException>(() => AssetImageRules.ValidatePng(data));
        }
        [Fact]
        public void DimensionsAndDecompressedPixelLengthAreBounded()
        {
            Assert.Throws<InputValidationException>(() => AssetImageRules.ValidatePng(TestPng.Create(width: 257)));
            Assert.Throws<InputValidationException>(() => AssetImageRules.ValidatePng(TestPng.Create(extraPixels: 100000)));
            AssetImageRules.ValidatePng(TestPng.Create(width: 128, height: 128));
        }
    }
}
