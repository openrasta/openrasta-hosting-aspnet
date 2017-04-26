﻿using System;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRasta.Hosting.InMemory;
using OpenRasta.Pipeline;
using OpenRasta.Web;
using Shouldly;

namespace OpenRasta.Hosting.AspNet.Tests.Integration
{
  public class AsyncYielding
  {
    [Test]
    public async Task middleware_yielding_same_thread()
    {
      var didItYield = await InvokeTillYield(
        new YieldingMiddleware(nameof(YieldingMiddleware)),
        new CodeMiddleware(() => Resumed = true));

      didItYield.ShouldBeTrue();
      Resumed.ShouldBeFalse();

      await Resume();

      Resumed.ShouldBeTrue();
    }

    [Test]
    public async Task middleware_yielding_other_thread()
    {
      var didItYield = await InvokeTillYield(
        new OtherThreadMiddleware(),
        new YieldingMiddleware(nameof(YieldingMiddleware)),
        new CodeMiddleware(()=>Resumed = true)
        );


      didItYield.ShouldBeTrue();
      Resumed.ShouldBeFalse();

      await Resume();

      Resumed.ShouldBeTrue();
    }

    [Test]
    public async Task middleware_yielding_before_code_on_other_thread()
    {
      var didItYield = await InvokeTillYield(
        new YieldingMiddleware(nameof(YieldingMiddleware)),
        new OtherThreadMiddleware(),
        new CodeMiddleware(()=>Resumed = true)
      );


      didItYield.ShouldBeTrue();
      Resumed.ShouldBeFalse();

      await Resume();

      Resumed.ShouldBeTrue();
    }

    [Test]
    public async Task middleware_not_yielding_same_thread()
    {
      var didItYield = await InvokeTillYield(
        new BypassingCodeMiddleware(),
        new YieldingMiddleware(nameof(YieldingMiddleware)),
        new CodeMiddleware(()=>Resumed = true)
      );


      didItYield.ShouldBeFalse();
      Resumed.ShouldBeFalse();

      await Resume();

      Resumed.ShouldBeFalse();
    }

    [Test]
    public async Task not_yielding_different_thread()
    {

      var didItYield = await InvokeTillYield(
        new OtherThreadMiddleware(),
        new BypassingCodeMiddleware(),
        new YieldingMiddleware(nameof(YieldingMiddleware)),
        new CodeMiddleware(()=>Resumed = true)
      );


      didItYield.ShouldBeFalse();
      Resumed.ShouldBeFalse();

      await Resume();

      Resumed.ShouldBeFalse();
    }


    ICommunicationContext Env;

    [SetUp]
    public void set_environment()
    {
      Env = new InMemoryCommunicationContext();
      Resumed = false;
    }

    bool Resumed { get; set; }
    async Task Resume()
    {
      Env.Resumer(nameof(YieldingMiddleware)).SetResult(true);
      await Operation;
    }

    async Task<bool> InvokeTillYield(params IPipelineMiddlewareFactory[] factories)
    {
      Operation = factories.Compose().Invoke(Env);

      return await Yielding.DidItYield(
        Operation,
        Env.Yielder(nameof(YieldingMiddleware)).Task);
    }

    Task Operation { get; set; }
  }

  class BypassingCodeMiddleware : IPipelineMiddleware, IPipelineMiddlewareFactory
  {
    public Task Invoke(ICommunicationContext env)
    {
      return Task.FromResult(true);
    }

    public IPipelineMiddleware Compose(IPipelineMiddleware next)
    {
      return this;
    }
  }

  class OtherThreadMiddleware : IPipelineMiddleware, IPipelineMiddlewareFactory
  {
    public Task Invoke(ICommunicationContext env)
    {
      return Task.Run(()=> Next.Invoke(env));
    }

    public IPipelineMiddleware Compose(IPipelineMiddleware next)
    {
      Next = next;
      return this;
    }

    IPipelineMiddleware Next { get; set; }
  }
  class CodeMiddleware : IPipelineMiddleware, IPipelineMiddlewareFactory
  {
    readonly Action _action;

    public CodeMiddleware(Action action)
    {
      _action = action;
    }

    public Task Invoke(ICommunicationContext env)
    {
      _action();
      return Task.FromResult(true);
    }

    public IPipelineMiddleware Compose(IPipelineMiddleware next)
    {
      return this;
    }
  }
}