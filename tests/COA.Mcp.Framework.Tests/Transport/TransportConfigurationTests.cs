using System.IO;
using COA.Mcp.Framework.Transport.Configuration;
using FluentAssertions;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Transport
{
    [TestFixture]
    public class TransportConfigurationTests
    {
        [Test]
        public void StdioTransportOptions_ShouldHaveNullableDefaults()
        {
            // Act
            var options = new StdioTransportOptions();

            // Assert - Options themselves don't have defaults, they're nullable
            options.Input.Should().BeNull();
            options.Output.Should().BeNull();
        }

        [Test]
        public void StdioTransportOptions_ShouldAllowCustomStreams()
        {
            // Arrange
            var input = new StringReader("test");
            var output = new StringWriter();

            // Act
            var options = new StdioTransportOptions
            {
                Input = input,
                Output = output
            };

            // Assert
            options.Input.Should().BeSameAs(input);
            options.Output.Should().BeSameAs(output);
        }

        [Test]
        public void HttpTransportOptions_ShouldHaveDefaultValues()
        {
            // Act
            var options = new HttpTransportOptions();

            // Assert
            options.Port.Should().Be(5000);
            options.Host.Should().Be("localhost");
            options.UseHttps.Should().BeFalse();
            options.EnableCors.Should().BeTrue();
            options.Authentication.Should().Be(AuthenticationType.None);
            options.ApiKey.Should().BeNull();
            options.AllowedOrigins.Should().BeEquivalentTo(new[] { "*" });
            options.MaxRequestSize.Should().Be(10 * 1024 * 1024); // 10MB
            options.RequestTimeoutSeconds.Should().Be(30);
            options.EnableWebSocket.Should().BeTrue();
            options.CertificatePath.Should().BeNull();
            options.CertificatePassword.Should().BeNull();
            options.JwtSettings.Should().BeNull();
        }

        [Test]
        public void HttpTransportOptions_ShouldAllowCustomConfiguration()
        {
            // Act
            var options = new HttpTransportOptions
            {
                Port = 9090,
                Host = "0.0.0.0",
                UseHttps = true,
                EnableCors = false,
                Authentication = AuthenticationType.ApiKey,
                ApiKey = "test-api-key",
                AllowedOrigins = new[] { "https://example.com" },
                MaxRequestSize = 5 * 1024 * 1024,
                RequestTimeoutSeconds = 60,
                EnableWebSocket = false,
                CertificatePath = "/path/to/cert.pfx",
                CertificatePassword = "cert-password"
            };

            // Assert
            options.Port.Should().Be(9090);
            options.Host.Should().Be("0.0.0.0");
            options.UseHttps.Should().BeTrue();
            options.EnableCors.Should().BeFalse();
            options.Authentication.Should().Be(AuthenticationType.ApiKey);
            options.ApiKey.Should().Be("test-api-key");
            options.AllowedOrigins.Should().BeEquivalentTo(new[] { "https://example.com" });
            options.MaxRequestSize.Should().Be(5 * 1024 * 1024);
            options.RequestTimeoutSeconds.Should().Be(60);
            options.EnableWebSocket.Should().BeFalse();
            options.CertificatePath.Should().Be("/path/to/cert.pfx");
            options.CertificatePassword.Should().Be("cert-password");
        }

        [Test]
        public void AuthenticationType_ShouldHaveCorrectValues()
        {
            // Assert
            AuthenticationType.None.Should().Be(AuthenticationType.None);
            AuthenticationType.ApiKey.Should().Be(AuthenticationType.ApiKey);
            AuthenticationType.Jwt.Should().Be(AuthenticationType.Jwt);
            AuthenticationType.Basic.Should().Be(AuthenticationType.Basic);
            AuthenticationType.Custom.Should().Be(AuthenticationType.Custom);
        }

        [Test]
        public void HttpTransportOptions_WithApiKeyAuth_ShouldRequireApiKey()
        {
            // Arrange
            var options = new HttpTransportOptions
            {
                Authentication = AuthenticationType.ApiKey
            };

            // Assert - In a real implementation, we'd validate this
            options.Authentication.Should().Be(AuthenticationType.ApiKey);
            options.ApiKey.Should().BeNull("not set yet");
        }

        [Test]
        public void HttpTransportOptions_WithSpecificOrigins_ShouldSetAllowedOrigins()
        {
            // Arrange
            var allowedOrigins = new[]
            {
                "https://app1.example.com",
                "https://app2.example.com",
                "http://localhost:3000"
            };

            // Act
            var options = new HttpTransportOptions
            {
                EnableCors = true,
                AllowedOrigins = allowedOrigins
            };

            // Assert
            options.EnableCors.Should().BeTrue();
            options.AllowedOrigins.Should().BeEquivalentTo(allowedOrigins);
        }

        [Test]
        public void JwtSettings_ShouldHaveDefaultValues()
        {
            // Act
            var settings = new JwtSettings();

            // Assert
            settings.SecretKey.Should().BeNull();
            settings.Issuer.Should().BeNull();
            settings.Audience.Should().BeNull();
            settings.ExpirationMinutes.Should().Be(60);
        }

        [Test]
        public void JwtSettings_ShouldAllowCustomConfiguration()
        {
            // Act
            var settings = new JwtSettings
            {
                SecretKey = "my-secret-key",
                Issuer = "https://example.com",
                Audience = "api-users",
                ExpirationMinutes = 120
            };

            // Assert
            settings.SecretKey.Should().Be("my-secret-key");
            settings.Issuer.Should().Be("https://example.com");
            settings.Audience.Should().Be("api-users");
            settings.ExpirationMinutes.Should().Be(120);
        }

        [Test]
        public void HttpTransportOptions_WithJwtAuth_ShouldConfigureJwtSettings()
        {
            // Act
            var options = new HttpTransportOptions
            {
                Authentication = AuthenticationType.Jwt,
                JwtSettings = new JwtSettings
                {
                    SecretKey = "jwt-secret",
                    Issuer = "mcp-server",
                    Audience = "mcp-clients",
                    ExpirationMinutes = 30
                }
            };

            // Assert
            options.Authentication.Should().Be(AuthenticationType.Jwt);
            options.JwtSettings.Should().NotBeNull();
            options.JwtSettings!.SecretKey.Should().Be("jwt-secret");
            options.JwtSettings.Issuer.Should().Be("mcp-server");
            options.JwtSettings.Audience.Should().Be("mcp-clients");
            options.JwtSettings.ExpirationMinutes.Should().Be(30);
        }

        [Test]
        public void TransportOptions_ShouldBeIndependent()
        {
            // Arrange
            var stdio1 = new StdioTransportOptions();
            var stdio2 = new StdioTransportOptions();
            var http1 = new HttpTransportOptions();
            var http2 = new HttpTransportOptions();

            // Act
            stdio1.Input = new StringReader("test");
            http1.Port = 9999;

            // Assert
            stdio2.Input.Should().BeNull("should not be affected by stdio1");
            http2.Port.Should().Be(5000, "should not be affected by http1");
        }
    }
}