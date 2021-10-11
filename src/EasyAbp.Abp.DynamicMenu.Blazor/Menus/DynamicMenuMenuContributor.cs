﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EasyAbp.Abp.DynamicMenu.Localization;
using EasyAbp.Abp.DynamicMenu.MenuItems;
using EasyAbp.Abp.DynamicMenu.MenuItems.Dtos;
using EasyAbp.Abp.DynamicMenu.Permissions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Security.Claims;
using Volo.Abp.UI.Navigation;

namespace EasyAbp.Abp.DynamicMenu.Blazor.Menus
{
    public class DynamicMenuMenuContributor : IMenuContributor
    {
        private ILogger<DynamicMenuMenuContributor> _logger;
        private IAbpAuthorizationPolicyProvider _policyProvider;
        private ICurrentPrincipalAccessor _currentPrincipalAccessor;
        private IMenuItemAppService _menuItemAppService;
        private IDynamicMenuStringLocalizerProvider _stringLocalizerProvider;

        private Dictionary<string, IStringLocalizer> ModuleNameStringLocalizers { get; } = new();
        
        public async Task ConfigureMenuAsync(MenuConfigurationContext context)
        {
            var loggerFactory = context.ServiceProvider.GetRequiredService<ILoggerFactory>();
            _logger = loggerFactory.CreateLogger<DynamicMenuMenuContributor>();
            _policyProvider = context.ServiceProvider.GetRequiredService<IAbpAuthorizationPolicyProvider>();
            _currentPrincipalAccessor = context.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>();
            _menuItemAppService = context.ServiceProvider.GetRequiredService<IMenuItemAppService>();
            _stringLocalizerProvider = context.ServiceProvider.GetRequiredService<IDynamicMenuStringLocalizerProvider>();
            
            if (context.Menu.Name == StandardMenus.Main)
            {
                await ConfigureMainMenuAsync(context);
            }
        }

        protected virtual async Task ConfigureMainMenuAsync(MenuConfigurationContext context)
        {
            var menuItems = await _menuItemAppService.GetListAsync(new GetMenuItemListInput
            {
                MaxResultCount = LimitedResultRequestDto.MaxMaxResultCount
            });

            await AddDynamicMenuItemsAsync(context.Menu, menuItems.Items, context);
            await AddDynamicMenuManagementMenuItemAsync(context);
        }

        protected virtual async Task AddDynamicMenuManagementMenuItemAsync(MenuConfigurationContext context)
        {
            var l = context.GetLocalizer<DynamicMenuResource>();
            //Add main menu items.
            context.Menu.AddItem(new ApplicationMenuItem(DynamicMenuMenus.Prefix, displayName: "DynamicMenu", "~/Abp/DynamicMenu"));

            if (await context.IsGrantedAsync(DynamicMenuPermissions.MenuItem.Default))
            {
                context.Menu.AddItem(
                    new ApplicationMenuItem(DynamicMenuMenus.MenuItem, l["Menu:MenuItem"], "~/Abp/DynamicMenu/MenuItems/MenuItem")
                );
            }
        }

        protected virtual async Task AddDynamicMenuItemsAsync(IHasMenuItems parent, IEnumerable<MenuItemDto> menuItems,
            MenuConfigurationContext context)
        {
            foreach (var menuItem in menuItems)
            {
                if (menuItem.Permission != null && !await IsFoundAndGrantedAsync(menuItem.Permission, context))
                {
                    continue;
                }

                var l = await GetOrCreateStringLocalizerAsync(menuItem, _stringLocalizerProvider);

                var child = new ApplicationMenuItem(menuItem.Name, l[menuItem.DisplayName]);

                if (menuItem.MenuItems.IsNullOrEmpty())
                {
                    if (menuItem.ParentName.IsNullOrEmpty())
                    {
                        continue;
                    }

                    child.Url = menuItem.UrlBlazor ?? menuItem.Url;
                }
                else
                {
                    await AddDynamicMenuItemsAsync(child, menuItem.MenuItems, context);
                }

                parent.Items.Add(child);
            }
        }

        protected virtual async Task<bool> IsFoundAndGrantedAsync(string policyName, MenuConfigurationContext context)
        {
            if (policyName == null)
            {
                throw new ArgumentNullException(nameof(policyName));
            }

            var policy = await _policyProvider.GetPolicyAsync(policyName);
            
            if (policy == null)
            {
                _logger.LogWarning($"[Entity UI] No policy found: {policyName}.");
                
                return false;
            }
            
            return (await context.AuthorizationService.AuthorizeAsync(
                _currentPrincipalAccessor.Principal,
                null,
                policyName)).Succeeded;
        }

        protected virtual async Task<IStringLocalizer> GetOrCreateStringLocalizerAsync(MenuItemDto menuItem,
            IDynamicMenuStringLocalizerProvider stringLocalizerProvider)
        {
            if (ModuleNameStringLocalizers.ContainsKey(menuItem.Name))
            {
                return ModuleNameStringLocalizers[menuItem.Name];
            }

            var localizer =  await stringLocalizerProvider.GetAsync(menuItem);

            ModuleNameStringLocalizers[menuItem.Name] = localizer;

            return localizer;
        }
    }
}