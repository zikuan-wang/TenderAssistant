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

    private const string PrivateKeyPem = """
-----BEGIN PRIVATE KEY-----
MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDAHQORFvIJGrv4
zZPeoTK3cwU5kTqfE5hVvKURD9ApM5cBtDd7qP2+8bFZFiMRRSKvDkaB84Pq1R5g
sOuNsRB/KLw0CSY45HrPIphCbrIOzMOJ1UUKdcQ4h6dQ+ixPeIZ0DG2w+gxy2112
i6ULZhH8Hb0cWYm1vUF0uMp/sLQtbmvIzLdp9i+Lr4Auebm56sIUb4ihmPUyWu+h
kJjIOkKrcCSQguSZChh4PAJOnupawDZF1XlU+dz2/E79UU981jBX8ncD/1b1WiDu
Sqbjqp7+xYnTMHlDZLckiNif7BF9t9RXaLOwHR73g3oiE1S71Hz5D+dVwQXiPK3I
FwEKg4EfAgMBAAECggEAB6xhAGzpMqHlu39X66mGoWXaYefsw9PViX8lHLRZUnv5
XnEN/PDNXyYU465gV4fqZbxCkV2M+avqMF8rWW0mxTLMVEu/7L/Q2UXUImXSKN3W
5Zd7es/PYZlRfojaN5EPKxgf/X9PUKXLFY69reHgJVt5ZTt8+6Gujp9JlKYV3Eae
DGZo7OtPwENusW+rBMXRAjlA3UDf3tuvVL7YFY60PBTSLSQi2admrFanweZRlmDT
iTK1nDmVoC7dlc4kXHIEak9/hYgH9r0NVQxq2CSKH7pwbbML4LutFX++uzM8MFbL
79SW7yemr2KqSREotu9JTIi1yoWqSuckd6VngcoGQQKBgQDt41UGb5xg84Mjfl3W
BxNpu/FWmGtQLb+ZKsYjDmHvWcdpd4i4FjYZO1Bl4532SMIhKqa4u7e/ySLpTHtX
wv0QI27iZuila7IJaeM06cfEGlzZSwXhwjedwp+WfWT5XXDtOlEXeJeIhgrOqii0
gGcKzUbzDNdq7M8o9gTwxuWJIQKBgQDOvX0rMSi+wqHBXdOKVY8YL9lVa+GL4LGN
QOM4G28/IOsLbNtFVuSGe+9JCmfJyVJLSIlUxH+DNEF51iffajRRoDJs57gK8lzx
uaFRpfirmPPOisKsx9yhTZ5jdNfvMNaz3zNPTuc8woLGa+BpQtPRDT0jTW6Hkmoo
U0Xhh+SCPwKBgE+yTZXuZnGEo3aMq1s826AcuunL/ofKC9qAngi4lM7fQRNwXHlv
14f0eybnbtBH5+G8rEZPfWvfMrb+TIRGawmxFUD8QQzKW8cTlm7vs2Fbg6e4nqvX
qVJNFbIRKHbyexa+5tP6LqoqXgyGrURrkBnqU86xiqnj1DNg2J7hw5yBAoGAOMNa
PKOwtj+mPftO+6pmMZPhrkyCju9QkKICQQN3VfPp1Sc8RRuIf0xD9OAAgyzdhYIT
As043YNZfuRH6lW0q7y6W6B7rbXBwzTekBZr4mGKf2kl7l6puzgehtwr9aaJLLXZ
1qqpXsthMK2p3fzVP47M/IZkFKEkuJG0nCm9me0CgYEAkEDAQm885bttv4LUnuAf
Z8Vhxl8MMIu1kFq3RLt16Izof6NdtCwtr7Apad2LcpgNA3GnqLbNTxFTGYaCRgBh
Sw8mWUhN6TkeQY7DG/sRiOaehm7ii6R51Ybo2vpRTGrCfUgA93yWeejZupy2hOeJ
Blb1nG1tq0aPXVV5q4v/Pzg=
-----END PRIVATE KEY-----
""";

    public static LicenseEnvelope Sign(LicensePayload payload)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(PrivateKeyPem);

        var data = Encoding.UTF8.GetBytes(OfflineLicenseJson.SerializePayload(payload));
        var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return new LicenseEnvelope(payload, Convert.ToBase64String(signature), Algorithm, KeyId);
    }

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
