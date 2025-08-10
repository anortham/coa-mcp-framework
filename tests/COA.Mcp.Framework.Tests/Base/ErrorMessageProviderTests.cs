using System;
using NUnit.Framework;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;

namespace COA.Mcp.Framework.Tests.Base;

[TestFixture]
public class ErrorMessageProviderTests
{
    private ErrorMessageProvider _provider;

    [SetUp]
    public void Setup()
    {
        _provider = new DefaultErrorMessageProvider();
    }

    [Test]
    public void ValidationFailed_ReturnsFormattedMessage()
    {
        var result = _provider.ValidationFailed("testParam", "must be positive");
        
        Assert.That(result, Is.EqualTo("Parameter 'testParam' validation failed: must be positive"));
    }

    [Test]
    public void ToolExecutionFailed_ReturnsFormattedMessage()
    {
        var result = _provider.ToolExecutionFailed("TestTool", "connection timeout");
        
        Assert.That(result, Is.EqualTo("Tool 'TestTool' execution failed: connection timeout"));
    }

    [Test]
    public void ParameterRequired_ReturnsFormattedMessage()
    {
        var result = _provider.ParameterRequired("requiredParam");
        
        Assert.That(result, Is.EqualTo("Parameter 'requiredParam' is required"));
    }

    [Test]
    public void RangeValidationFailed_ReturnsFormattedMessage()
    {
        var result = _provider.RangeValidationFailed("count", 1, 100);
        
        Assert.That(result, Is.EqualTo("Parameter 'count' must be between 1 and 100"));
    }

    [Test]
    public void MustBePositive_ReturnsFormattedMessage()
    {
        var result = _provider.MustBePositive("amount");
        
        Assert.That(result, Is.EqualTo("Parameter 'amount' must be positive"));
    }

    [Test]
    public void CannotBeEmpty_ReturnsFormattedMessage()
    {
        var result = _provider.CannotBeEmpty("list");
        
        Assert.That(result, Is.EqualTo("Parameter 'list' cannot be empty"));
    }

    [Test]
    public void GetRecoveryInfo_ValidationError_ReturnsSteps()
    {
        var result = _provider.GetRecoveryInfo("VALIDATION_ERROR");
        
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Steps, Has.Length.GreaterThan(0));
        Assert.That(result.Steps[0], Does.Contain("parameter requirements"));
    }

    [Test]
    public void GetRecoveryInfo_ToolError_ReturnsSteps()
    {
        var result = _provider.GetRecoveryInfo("TOOL_ERROR");
        
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Steps, Has.Length.GreaterThan(0));
        Assert.That(result.Steps[0], Does.Contain("error message"));
    }

    [Test]
    public void GetRecoveryInfo_Timeout_ReturnsSteps()
    {
        var result = _provider.GetRecoveryInfo("TIMEOUT");
        
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Steps, Has.Length.GreaterThan(0));
        Assert.That(result.Steps[0], Does.Contain("smaller input"));
    }

    [Test]
    public void GetRecoveryInfo_UnknownCode_ReturnsDefaultSteps()
    {
        var result = _provider.GetRecoveryInfo("UNKNOWN_ERROR");
        
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Steps, Has.Length.EqualTo(1));
        Assert.That(result.Steps[0], Does.Contain("retry"));
    }

    [Test]
    public void CustomErrorMessageProvider_OverridesMessages()
    {
        var customProvider = new TestCustomErrorMessageProvider();
        
        var message = customProvider.ValidationFailed("param", "requirement");
        
        Assert.That(message, Is.EqualTo("[CUSTOM] Parameter 'param' failed: requirement"));
    }

    private class TestCustomErrorMessageProvider : ErrorMessageProvider
    {
        public override string ValidationFailed(string paramName, string requirement)
        {
            return $"[CUSTOM] Parameter '{paramName}' failed: {requirement}";
        }
    }
}