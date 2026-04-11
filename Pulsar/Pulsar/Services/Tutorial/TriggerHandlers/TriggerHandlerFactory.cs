// [Path]: Pulsar/Pulsar/Services/Tutorial/TriggerHandlers/TriggerHandlerFactory.cs

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Pulsar.Models.Tutorial;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;

namespace Pulsar.Services.Tutorial.TriggerHandlers
{
    /// <summary>
    /// 触发器处理器工厂实现
    /// 使用工厂模式创建触发器处理器，支持插件化扩展
    /// </summary>
    public class TriggerHandlerFactory : ITriggerHandlerFactory
    {
        private readonly Dictionary<TutorialTriggerType, Func<ITriggerHandler>> _factories = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TriggerHandlerFactory> _logger;

        public TriggerHandlerFactory(
            IServiceProvider serviceProvider,
            ILogger<TriggerHandlerFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            
            RegisterDefaultHandlers();
        }

        /// <summary>
        /// 注册默认的触发器处理器
        /// </summary>
        private void RegisterDefaultHandlers()
        {
            _logger.LogInformation("Registering default trigger handlers");

            // ButtonClick - 不需要依赖，由 TutorialStepCard 直接处理
            RegisterHandler(TutorialTriggerType.ButtonClick, () => 
            {
                _logger.LogDebug("ButtonClick trigger is handled by TutorialStepCard");
                return null!; // ButtonClick 由卡片自己处理
            });

            // WindowOpened - 无依赖
            RegisterHandler(TutorialTriggerType.WindowOpened, () => 
                new WindowOpenedTriggerHandler());

            // PageNavigated - 需要 SettingsViewModel
            RegisterHandler(TutorialTriggerType.PageNavigated, () =>
            {
                var settingsViewModel = GetService<SettingsViewModel>();
                return new PageNavigatedTriggerHandler(settingsViewModel);
            });

            // NavigationItemClicked - 需要在运行时获取 NavigationView
            // 这个处理器需要特殊处理，在 TutorialOrchestrator 中创建
            RegisterHandler(TutorialTriggerType.NavigationItemClicked, () =>
            {
                _logger.LogDebug("NavigationItemClicked requires runtime NavigationView, created in orchestrator");
                return null!; // 需要在 orchestrator 中特殊处理
            });

            RegisterHandler(TutorialTriggerType.ActionExecuted, () => new ActionExecutedTriggerHandler());

            // SlotAdded - 需要 IConfigService 和 ILogger
            RegisterHandler(TutorialTriggerType.SlotAdded, () =>
            {
                var configService = GetService<IConfigService>();
                var logger = GetService<ILogger<SlotAddedTriggerHandler>>();
                return new SlotAddedTriggerHandler(configService, logger);
            });

            // ProfileConfigured - 需要 IConfigService 和 ILogger
            RegisterHandler(TutorialTriggerType.ProfileConfigured, () =>
            {
                var configService = GetService<IConfigService>();
                var logger = GetService<ILogger<ProfileConfiguredTriggerHandler>>();
                return new ProfileConfiguredTriggerHandler(configService, logger);
            });

            // RadialMenuShown - 需要 RadialMenuViewModel
            RegisterHandler(TutorialTriggerType.RadialMenuShown, () =>
            {
                var radialMenuViewModel = GetService<RadialMenuViewModel>();
                return new RadialMenuShownTriggerHandler(radialMenuViewModel);
            });

            _logger.LogInformation("Registered {Count} default trigger handlers", _factories.Count);
        }

        /// <summary>
        /// 创建触发器处理器
        /// </summary>
        public ITriggerHandler? CreateHandler(TutorialTriggerType type)
        {
            if (!_factories.TryGetValue(type, out var factory))
            {
                _logger.LogWarning("No handler factory registered for trigger type: {Type}", type);
                return null;
            }

            try
            {
                var handler = factory();
                
                if (handler != null)
                {
                    _logger.LogDebug("Created trigger handler for type: {Type}", type);
                }
                
                return handler;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create trigger handler for type: {Type}", type);
                return null;
            }
        }

        /// <summary>
        /// 注册自定义触发器处理器
        /// </summary>
        public void RegisterHandler(TutorialTriggerType type, Func<ITriggerHandler> factory)
        {
            _factories[type] = factory;
            _logger.LogDebug("Registered handler factory for trigger type: {Type}", type);
        }

        /// <summary>
        /// 检查是否支持指定的触发器类型
        /// </summary>
        public bool IsSupported(TutorialTriggerType type)
        {
            return _factories.ContainsKey(type);
        }

        /// <summary>
        /// 从 DI 容器获取服务
        /// </summary>
        private T GetService<T>() where T : class
        {
            var service = _serviceProvider.GetService(typeof(T)) as T;
            
            if (service == null)
            {
                throw new InvalidOperationException(
                    $"Required service {typeof(T).Name} is not registered in DI container");
            }
            
            return service;
        }
    }
}
