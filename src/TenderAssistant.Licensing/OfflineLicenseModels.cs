using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TenderAssistant.Licensing;

public sealed record OfflineLicenseRequest(
    Guid RequestId,
    string ApplicantName,
    string MachineName,
    string DeviceFingerprint,
    DateTimeOffset CreatedAtUtc);

public sealed record LicensePayload(
    string LicenseId,
    string CustomerName,
    string DeviceFingerprint,
    IReadOnlyList<string> Modules,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string Edition);

public sealed record LicenseEnvelope(
    LicensePayload Payload,
    string Signature,
    string Algorithm,
    string KeyId);

public sealed record LicenseValidationResult(
    bool IsValid,
    string Status,
    string Message,
    LicensePayload? Payload,
    DateTimeOffset CheckedAtUtc);

public static class OfflineLicenseJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string SerializeRequest(OfflineLicenseRequest request)
    {
        return JsonSerializer.Serialize(request, Options);
    }

    public static OfflineLicenseRequest? DeserializeRequest(string json)
    {
        return JsonSerializer.Deserialize<OfflineLicenseRequest>(json, Options);
    }

    public static string SerializePayload(LicensePayload payload)
    {
        return JsonSerializer.Serialize(payload, Options);
    }

    public static string SerializeEnvelope(LicenseEnvelope envelope)
    {
        return JsonSerializer.Serialize(envelope, Options);
    }

    public static LicenseEnvelope? DeserializeEnvelope(string json)
    {
        return JsonSerializer.Deserialize<LicenseEnvelope>(json, Options);
    }
}

public static class OfflineLicenseCrypto
{
    public const string Algorithm = "RS256";
    public const string KeyId = "offline-rsa-2026-05";
    private const string ActivationHeader = "BALI1.";
    private const int AesNonceSize = 12;
    private const int AesTagSize = 16;
    private static readonly byte[] ActivationAdditionalData = Encoding.UTF8.GetBytes("TenderAssistant.BALI.v1");

    private const string PublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAwB0DkRbyCRq7+M2T3qEy
t3MFOZE6nxOYVbylEQ/QKTOXAbQ3e6j9vvGxWRYjEUUirw5GgfOD6tUeYLDrjbEQ
fyi8NAkmOOR6zyKYQm6yDszDidVFCnXEOIenUPosT3iGdAxtsPoMcttddoulC2YR
/B29HFmJtb1BdLjKf7C0LW5ryMy3afYvi6+ALnm5uerCFG+IoZj1MlrvoZCYyDpC
q3AkkILkmQoYeDwCTp7qWsA2RdV5VPnc9vxO/VFPfNYwV/J3A/9W9Vog7kqm46qe
/sWJ0zB5Q2S3JIjYn+wRfbfUV2izsB0e94N6IhNUu9R8+Q/nVcEF4jytyBcBCoOB
HwIDAQAB
-----END PUBLIC KEY-----
""";

    public static string EncryptEnvelope(LicenseEnvelope envelope)
    {
        var plainText = Encoding.UTF8.GetBytes(OfflineLicenseJson.SerializeEnvelope(envelope));
        var nonce = RandomNumberGenerator.GetBytes(AesNonceSize);
        var cipherText = new byte[plainText.Length];
        var tag = new byte[AesTagSize];

        using var aes = new AesGcm(GetActivationKey(), AesTagSize);
        aes.Encrypt(nonce, plainText, cipherText, tag, ActivationAdditionalData);

        var package = new byte[nonce.Length + tag.Length + cipherText.Length];
        Buffer.BlockCopy(nonce, 0, package, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, package, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherText, 0, package, nonce.Length + tag.Length, cipherText.Length);
        return ActivationHeader + Convert.ToBase64String(package);
    }

    public static LicenseEnvelope? DecryptEnvelope(string encryptedContent)
    {
        if (string.IsNullOrWhiteSpace(encryptedContent) || !encryptedContent.StartsWith(ActivationHeader, StringComparison.Ordinal))
        {
            return null;
        }

        var package = Convert.FromBase64String(encryptedContent[ActivationHeader.Length..].Trim());
        if (package.Length <= AesNonceSize + AesTagSize)
        {
            return null;
        }

        var nonce = package[..AesNonceSize];
        var tag = package[AesNonceSize..(AesNonceSize + AesTagSize)];
        var cipherText = package[(AesNonceSize + AesTagSize)..];
        var plainText = new byte[cipherText.Length];

        using var aes = new AesGcm(GetActivationKey(), AesTagSize);
        aes.Decrypt(nonce, cipherText, tag, plainText, ActivationAdditionalData);
        return OfflineLicenseJson.DeserializeEnvelope(Encoding.UTF8.GetString(plainText));
    }

    public static LicenseValidationResult Validate(LicenseEnvelope envelope, string deviceFingerprint)
    {
        var now = DateTimeOffset.UtcNow;
        if (!string.Equals(envelope.Algorithm, Algorithm, StringComparison.Ordinal))
        {
            return Invalid("unsupported_algorithm", "授权签名算法不受支持。", envelope.Payload, now);
        }

        if (!VerifySignature(envelope))
        {
            return Invalid("invalid_signature", "授权文件签名无效或内容已被修改。", envelope.Payload, now);
        }

        if (!string.Equals(envelope.Payload.DeviceFingerprint, deviceFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return Invalid("device_mismatch", "授权文件不属于当前设备。", envelope.Payload, now);
        }

        if (envelope.Payload.ExpiresAtUtc < now)
        {
            return Invalid("expired", "授权已过期。", envelope.Payload, now);
        }

        return new LicenseValidationResult(true, "active", "授权有效。", envelope.Payload, now);
    }

    private static bool VerifySignature(LicenseEnvelope envelope)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(PublicKeyPem);

            var data = Encoding.UTF8.GetBytes(OfflineLicenseJson.SerializePayload(envelope.Payload));
            var signature = Convert.FromBase64String(envelope.Signature);
            return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    private static LicenseValidationResult Invalid(string status, string message, LicensePayload? payload, DateTimeOffset checkedAtUtc)
    {
        return new LicenseValidationResult(false, status, message, payload, checkedAtUtc);
    }

    private static byte[] GetActivationKey()
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes("TenderAssistant.BALI.OfflineActivation.2026"));
    }
}
