using System;
using System.Collections.Generic;
using Brutal.VulkanApi;
using OniMoono.Desktop.Vulkan;
using OniMoono.Host;
using Xunit;

namespace OniMoono.Tests
{
    /// The renderer option (settings file, command line) and the Vulkan device's GPU selection.
    public class RendererChoiceTests
    {
        [Fact]
        public void TheSettingsFileAndCommandLineChooseIt()
        {
            var o = EffectiveOptions.Resolve([], [], [], null, false, new PlayerSettings());
            Assert.Equal(("auto", OptionSource.Default), (o.Renderer, o.RendererSource));
            var file = PlayerSettings.Parse("""{ "renderer": "opengl" }""");
            o = EffectiveOptions.Resolve([], [], [], null, false, file);
            Assert.Equal(("opengl", OptionSource.SettingsFile), (o.Renderer, o.RendererSource));
            o = EffectiveOptions.Resolve([], [], [], null, false, file, commandLineRenderer: "vulkan");
            Assert.Equal(("vulkan", OptionSource.CommandLine), (o.Renderer, o.RendererSource));
            Assert.Contains("renderer vulkan (the command line)", o.Describe());
            Assert.Throws<FormatException>(() => PlayerSettings.Parse("""{ "renderer": "d3d11" }"""));
            // Saved only when it isn't the default; kept when the screen didn't set it.
            Assert.Equal("{\"renderer\":\"vulkan\"}", EffectiveOptions.Apply(new PlayerSettings(), [], new Dictionary<string, bool>(), null, null, renderer: "vulkan").ToJson(indented: false));
            Assert.Equal("{}", EffectiveOptions.Apply(file, [], new Dictionary<string, bool>(), null, null, renderer: "auto").ToJson(indented: false));
            Assert.Equal("{\"renderer\":\"opengl\"}", EffectiveOptions.Apply(file, [], new Dictionary<string, bool>(), null, null).ToJson(indented: false));
        }

        static readonly VkCandidate Discrete = new("RTX", VkPhysicalDeviceType.DiscreteGpu, VkContext.RequiredApi, null);
        static readonly VkCandidate Integrated = new("Radeon", VkPhysicalDeviceType.IntegratedGpu, VkContext.RequiredApi, null);
        static readonly VkCandidate Software = new("llvmpipe", VkPhysicalDeviceType.Cpu, VkContext.RequiredApi, null);
        static readonly VkCandidate Old = new("old", VkPhysicalDeviceType.DiscreteGpu, VkContext.RequiredApi, "Vulkan 1.1.0, 1.3 needed");

        [Fact]
        public void TheGpuOptionPicksByType()
        {
            Assert.Equal(1, VkContext.Choose([Integrated, Discrete], "default", false, out _));
            Assert.Equal(1, VkContext.Choose([Integrated, Discrete], "discrete", false, out _));
            Assert.Equal(0, VkContext.Choose([Integrated, Discrete], "integrated", false, out var reason));
            Assert.Equal("Radeon (integrated, gpu option integrated)", reason);
            // Integrated asked for on a machine with only a discrete GPU: that one.
            Assert.Equal(0, VkContext.Choose([Discrete, Software], "integrated", false, out _));
        }

        [Fact]
        public void AutoRefusesSoftwareAndDevicesMissingRequirements()
        {
            Assert.Equal(-1, VkContext.Choose([Software], "default", false, out var reason));
            Assert.Equal("llvmpipe: a software rasterizer (only used with --renderer vulkan)", reason);
            Assert.Equal(0, VkContext.Choose([Software], "default", true, out _));
            Assert.Equal(-1, VkContext.Choose([Old], "default", true, out reason));
            Assert.Equal("old: Vulkan 1.1.0, 1.3 needed", reason);
            Assert.Equal(1, VkContext.Choose([Old, Software], "discrete", true, out _));
            Assert.Equal(-1, VkContext.Choose([], "default", true, out reason));
            Assert.Equal("no Vulkan device", reason);
        }
    }
}
