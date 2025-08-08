using System.Collections.Generic;
using COA.Mcp.Framework.Server.Services;
using NUnit.Framework;

namespace COA.Mcp.Framework.Tests.Server.Services;

[TestFixture]
public class ServiceConfigurationTests
{
    [Test]
    public void ServiceConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new ServiceConfiguration();

        // Assert
        Assert.That(config.ServiceId, Is.EqualTo(string.Empty));
        Assert.That(config.ExecutablePath, Is.EqualTo(string.Empty));
        Assert.That(config.Arguments, Is.Empty);
        Assert.That(config.Port, Is.EqualTo(0));
        Assert.That(config.HealthEndpoint, Is.EqualTo(string.Empty));
        Assert.That(config.StartupTimeoutSeconds, Is.EqualTo(30));
        Assert.That(config.HealthCheckIntervalSeconds, Is.EqualTo(60));
        Assert.That(config.AutoRestart, Is.True);
        Assert.That(config.MaxRestartAttempts, Is.EqualTo(3));
        Assert.That(config.EnvironmentVariables, Is.Not.Null);
        Assert.That(config.EnvironmentVariables, Is.Empty);
        Assert.That(config.RedirectStandardOutput, Is.True);
        Assert.That(config.RedirectStandardError, Is.True);
        Assert.That(config.WorkingDirectory, Is.Null);
    }

    [Test]
    public void ServiceConfiguration_CanSetAllProperties()
    {
        // Arrange
        var config = new ServiceConfiguration();
        var envVars = new Dictionary<string, string> { { "TEST", "value" } };

        // Act
        config.ServiceId = "test-service";
        config.ExecutablePath = "/usr/bin/test";
        config.Arguments = new[] { "--arg1", "--arg2" };
        config.Port = 8080;
        config.HealthEndpoint = "http://localhost:8080/health";
        config.StartupTimeoutSeconds = 60;
        config.HealthCheckIntervalSeconds = 30;
        config.AutoRestart = false;
        config.MaxRestartAttempts = 5;
        config.EnvironmentVariables = envVars;
        config.RedirectStandardOutput = false;
        config.RedirectStandardError = false;
        config.WorkingDirectory = "/tmp";

        // Assert
        Assert.That(config.ServiceId, Is.EqualTo("test-service"));
        Assert.That(config.ExecutablePath, Is.EqualTo("/usr/bin/test"));
        Assert.That(config.Arguments, Has.Length.EqualTo(2));
        Assert.That(config.Arguments[0], Is.EqualTo("--arg1"));
        Assert.That(config.Arguments[1], Is.EqualTo("--arg2"));
        Assert.That(config.Port, Is.EqualTo(8080));
        Assert.That(config.HealthEndpoint, Is.EqualTo("http://localhost:8080/health"));
        Assert.That(config.StartupTimeoutSeconds, Is.EqualTo(60));
        Assert.That(config.HealthCheckIntervalSeconds, Is.EqualTo(30));
        Assert.That(config.AutoRestart, Is.False);
        Assert.That(config.MaxRestartAttempts, Is.EqualTo(5));
        Assert.That(config.EnvironmentVariables, Is.EqualTo(envVars));
        Assert.That(config.RedirectStandardOutput, Is.False);
        Assert.That(config.RedirectStandardError, Is.False);
        Assert.That(config.WorkingDirectory, Is.EqualTo("/tmp"));
    }

    [Test]
    public void ServiceConfiguration_EnvironmentVariables_CanBeModified()
    {
        // Arrange
        var config = new ServiceConfiguration();

        // Act
        config.EnvironmentVariables["VAR1"] = "value1";
        config.EnvironmentVariables["VAR2"] = "value2";

        // Assert
        Assert.That(config.EnvironmentVariables.Count, Is.EqualTo(2));
        Assert.That(config.EnvironmentVariables["VAR1"], Is.EqualTo("value1"));
        Assert.That(config.EnvironmentVariables["VAR2"], Is.EqualTo("value2"));
    }
}