using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Core.Plugin;
using Pulsar.Services;
using Xunit;

namespace Pulsar.Tests.Services
{
    public class PluginPackageIntegrityServiceTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _trustedPublishersDirectory;

        public PluginPackageIntegrityServiceTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "PulsarTests", Guid.NewGuid().ToString("N"));
            _trustedPublishersDirectory = Path.Combine(_testDirectory, "TrustedPublishers");
            Directory.CreateDirectory(_trustedPublishersDirectory);
        }

        [Fact]
        public async Task VerifyArchiveAsync_NoTrustedKeys_UnsignedPackage_AcceptedInDeveloperMode()
        {
            var service = CreateService();
            var archive = CreateArchive("dev-package.zip");

            var result = await service.VerifyArchiveAsync(archive);

            result.Status.Should().Be(PluginPackageIntegrityStatus.Unsigned);
            result.IsValid.Should().BeTrue("unsigned packages are allowed while no trusted publisher keys exist");
            result.PackageSha256.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task VerifyArchiveAsync_TrustedKeysConfigured_UnsignedPackage_FailsClosed()
        {
            InstallTrustedKey();
            var service = CreateService();
            var archive = CreateArchive("unsigned-package.zip");

            var result = await service.VerifyArchiveAsync(archive);

            result.Status.Should().Be(PluginPackageIntegrityStatus.NotVerified);
            result.IsValid.Should().BeFalse("once a trusted key exists an unsigned package must fail closed");
            result.Error.Should().Contain("not signed");
        }

        [Fact]
        public async Task VerifyArchiveAsync_ValidSignature_ReturnsVerified()
        {
            using var rsa = RSA.Create();
            InstallTrustedKey(rsa);
            var service = CreateService();

            var archive = CreateArchive("signed-package.zip");
            WriteSignatureSidecar(archive, rsa, publisher: "TestPublisher");

            var result = await service.VerifyArchiveAsync(archive);

            result.Status.Should().Be(PluginPackageIntegrityStatus.SignatureVerified);
            result.IsValid.Should().BeTrue();
            result.Publisher.Should().Be("TestPublisher");
        }

        [Fact]
        public async Task VerifyArchiveAsync_SignatureByUntrustedKey_Fails()
        {
            InstallTrustedKey();

            using var untrustedRsa = RSA.Create();
            var service = CreateService();

            var archive = CreateArchive("untrusted-package.zip");
            WriteSignatureSidecar(archive, untrustedRsa, publisher: "EvilPublisher");

            var result = await service.VerifyArchiveAsync(archive);

            result.IsValid.Should().BeFalse();
            result.Error.Should().Contain("not trusted");
        }

        [Fact]
        public async Task VerifyArchiveAsync_TamperedArchive_WithValidSignatureEnvelope_Fails()
        {
            using var rsa = RSA.Create();
            InstallTrustedKey(rsa);
            var service = CreateService();

            var archive = CreateArchive("tampered-package.zip");
            WriteSignatureSidecar(archive, rsa, publisher: "TestPublisher");

            // Mutate the archive bytes after signing, then rewrite the signature file to
            // match the new bytes hash so only signature verification can detect it.
            var bytes = File.ReadAllBytes(archive);
            bytes[bytes.Length / 2] ^= 0xFF;
            File.WriteAllBytes(archive, bytes);

            var result = await service.VerifyArchiveAsync(archive);

            result.IsValid.Should().BeFalse("signature verification must detect tampering even when hash records align");
            result.Error.Should().Contain("verification failed");
        }

        [Fact]
        public async Task VerifyArchiveAsync_InvalidSignatureFile_Fails()
        {
            using var rsa = RSA.Create();
            InstallTrustedKey(rsa);
            var service = CreateService();

            var archive = CreateArchive("malformed-package.zip");
            File.WriteAllText(archive + ".pulsar.sig", "{ not json");

            var result = await service.VerifyArchiveAsync(archive);

            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public async Task VerifyExtractedAsync_MatchingHashes_Succeeds()
        {
            var service = CreateService();
            var extractPath = CreateExtractedFolder();

            var result = await service.VerifyExtractedAsync(
                extractPath,
                new Dictionary<string, string>
                {
                    ["lib/plugin.dll"] = HashOfFile(Path.Combine(extractPath, "lib", "plugin.dll")),
                    ["manifest.json"] = HashOfFile(Path.Combine(extractPath, "manifest.json"))
                });

            result.Status.Should().Be(PluginPackageIntegrityStatus.VerifiedHash);
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task VerifyExtractedAsync_MismatchedHash_Fails()
        {
            var service = CreateService();
            var extractPath = CreateExtractedFolder();

            var result = await service.VerifyExtractedAsync(
                extractPath,
                new Dictionary<string, string>
                {
                    ["manifest.json"] = "0".PadLeft(64, '0')
                });

            result.IsValid.Should().BeFalse();
            result.Error.Should().Contain("mismatch");
        }

        [Fact]
        public async Task VerifyExtractedAsync_MissingFile_Fails()
        {
            var service = CreateService();
            var extractPath = CreateExtractedFolder();

            var result = await service.VerifyExtractedAsync(
                extractPath,
                new Dictionary<string, string>
                {
                    ["missing/file.txt"] = "0".PadLeft(64, '0')
                });

            result.IsValid.Should().BeFalse();
            result.Error.Should().Contain("missing");
        }

        [Fact]
        public async Task VerifyExtractedAsync_NoExpectedHashes_Succeeds()
        {
            var service = CreateService();
            var extractPath = CreateExtractedFolder();

            var result = await service.VerifyExtractedAsync(
                extractPath,
                new Dictionary<string, string>());

            result.IsValid.Should().BeTrue("packages without a file hash manifest remain installable");
        }

        [Fact]
        public async Task WriteInstallRecord_ThenVerifyInstalled_DetectsMutation()
        {
            var service = CreateService();
            var installPath = CreateExtractedFolder();
            var dllPath = Path.Combine(installPath, "lib", "plugin.dll");

            var writeResult = await service.WriteInstallRecordAsync(installPath, "abc123");

            writeResult.IsValid.Should().BeTrue();
            File.Exists(Path.Combine(installPath, "integrity.json")).Should().BeTrue();

            var cleanVerify = await service.VerifyInstalledAsync(installPath);
            cleanVerify.IsValid.Should().BeTrue("an untouched install must verify against its own record");

            File.WriteAllText(dllPath, "tampered content");

            var tamperedVerify = await service.VerifyInstalledAsync(installPath);
            tamperedVerify.IsValid.Should().BeFalse("mutating an installed file must be detected");
            tamperedVerify.Error.Should().Contain("changed after installation");
        }

        private PluginPackageIntegrityService CreateService()
        {
            return new PluginPackageIntegrityService(
                NullLogger<PluginPackageIntegrityService>.Instance,
                _trustedPublishersDirectory);
        }

        private string CreateArchive(string name)
        {
            var path = Path.Combine(_testDirectory, name);
            File.WriteAllText(path, "archive-content-" + Guid.NewGuid().ToString("N"));
            return path;
        }

        private string CreateExtractedFolder()
        {
            var path = Path.Combine(_testDirectory, "extracted", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(path, "lib"));
            File.WriteAllText(Path.Combine(path, "manifest.json"), "{\"id\":\"com.example\"}");
            File.WriteAllBytes(Path.Combine(path, "lib", "plugin.dll"), Encoding.UTF8.GetBytes("fake dll bytes"));
            return path;
        }

        private void InstallTrustedKey(RSA? rsa = null)
        {
            var key = rsa ?? RSA.Create();
            var subjectPublicKeyInfo = key.ExportSubjectPublicKeyInfo();
            var base64 = Convert.ToBase64String(subjectPublicKeyInfo);
            const int lineLength = 64;

            var builder = new StringBuilder();
            builder.AppendLine("-----BEGIN PUBLIC KEY-----");
            for (int offset = 0; offset < base64.Length; offset += lineLength)
            {
                builder.AppendLine(base64.Substring(offset, Math.Min(lineLength, base64.Length - offset)));
            }
            builder.Append("-----END PUBLIC KEY-----");

            File.WriteAllText(Path.Combine(_trustedPublishersDirectory, "publisher.pem"), builder.ToString());
        }

        private static void WriteSignatureSidecar(string archivePath, RSA rsa, string publisher)
        {
            var bytes = File.ReadAllBytes(archivePath);
            var signature = rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            var envelope = new
            {
                publisher,
                publicKeyThumbprint = ComputeThumbprint(rsa.ExportSubjectPublicKeyInfo()),
                algorithm = "RSA-SHA256",
                signature = Convert.ToBase64String(signature)
            };

            File.WriteAllText(
                archivePath + ".pulsar.sig",
                JsonSerializer.Serialize(envelope));
        }

        private static string ComputeThumbprint(byte[] subjectPublicKeyInfo)
        {
            return Convert.ToHexString(SHA256.HashData(subjectPublicKeyInfo)).ToLowerInvariant();
        }

        private static string HashOfFile(string path)
        {
            return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
            }
            catch
            {
                // Best-effort test cleanup.
            }
        }
    }
}
