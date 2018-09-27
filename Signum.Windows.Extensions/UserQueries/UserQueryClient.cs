﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Entities;
using Signum.Services;
using System.Reflection;
using Signum.Entities.DynamicQuery;
using Signum.Entities.UserQueries;
using Signum.Windows.Authorization;
using Signum.Entities.Authorization;
using Signum.Windows.Omnibox;
using System.Windows;
using Signum.Utilities;
using System.Windows.Controls;
using Signum.Entities.Chart;
using Signum.Windows.Basics;
using Signum.Entities.Reflection;
using Signum.Windows.UserAssets;
using Signum.Entities.UserAssets;

namespace Signum.Windows.UserQueries
{
    public class UserQueryClient
    {
        public static readonly DependencyProperty UserQueryProperty =
            DependencyProperty.RegisterAttached("UserQuery", typeof(UserQueryEntity), typeof(UserQueryClient), new FrameworkPropertyMetadata((s, e)=>OnUserQueryChanged(s, (UserQueryEntity)e.NewValue)));
        public static UserQueryEntity GetUserQuery(DependencyObject obj)
        {
            return (UserQueryEntity)obj.GetValue(UserQueryProperty);
        }

        public static void SetUserQuery(DependencyObject obj, UserQueryEntity value)
        {
            obj.SetValue(UserQueryProperty, value);
        }

        private static void OnUserQueryChanged(DependencyObject s, UserQueryEntity uc)
        {
            UserQueryPermission.ViewUserQuery.Authorize();

            var currentEntity = UserAssetsClient.GetCurrentEntity(s);

            if (s is CountSearchControl csc)
            {
                csc.QueryName = QueryClient.GetQueryName(uc.Query.Key);
                using (currentEntity == null ? null : CurrentEntityConverter.SetCurrentEntity(currentEntity))
                    UserQueryClient.ToCountSearchControl(uc, csc);
                csc.Search();
                return;
            }

            if (s is SearchControl sc && sc.ShowHeader == false)
            {
                sc.QueryName = QueryClient.GetQueryName(uc.Query.Key);
                using (currentEntity == null ? null : CurrentEntityConverter.SetCurrentEntity(currentEntity))
                    UserQueryClient.ToSearchControl(uc, sc);
                sc.Search();
                return;
            }

            return;
        }

        public static void Start()
        {
            if (Navigator.Manager.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                TypeClient.Start();
                QueryClient.Start();
                Navigator.AddSetting(new EntitySettings<UserQueryEntity> { View = _ => new UserQuery(), Icon = ExtensionsImageLoader.GetImageSortName("userQuery.png")  });
                SearchControl.GetMenuItems += SearchControl_GetCustomMenuItems;
                UserAssetsClient.Start();
                UserAssetsClient.RegisterExportAssertLink<UserQueryEntity>();

                Constructor.Register<UserQueryEntity>(ctx =>
                {
                    MessageBox.Show(Window.GetWindow(ctx.Element),
                        ChartMessage._0CanOnlyBeCreatedFromTheSearchWindow.NiceToString().FormatWith(typeof(UserQueryEntity).NicePluralName()),
                        ChartMessage.CreateNew.NiceToString(),
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return null;
                });

                LinksClient.RegisterEntityLinks<Entity>((entity, ctrl) =>
                {
                    if (!UserQueryPermission.ViewUserQuery.IsAuthorized())
                        return null;

                    return Server.Return((IUserQueryServer us) => us.GetUserQueriesEntity(entity.EntityType))
                        .Select(cp => new UserQueryQuickLink(cp, entity)).ToArray();
                });
            }
        }

        class UserQueryQuickLink : QuickLink
        {
            Lite<UserQueryEntity> userQuery;
            Lite<Entity> entity;

            public UserQueryQuickLink(Lite<UserQueryEntity> userQuery, Lite<Entity> entity)
            {
                this.ToolTip = userQuery.ToString();
                this.Label = userQuery.ToString();
                this.userQuery = userQuery;
                this.entity = entity;
                this.Icon = ExtensionsImageLoader.GetImageSortName("userQuery.png");
                this.IsVisible = true;
            }

            public override void Execute()
            {
                UserQueryClient.Explore(userQuery.Retrieve(), entity.Retrieve());
            }

            public override string Name
            {
                get { return userQuery.Key(); }
            }
        }


        static MenuItem SearchControl_GetCustomMenuItems(SearchControl seachControl)
        {
            if (!Navigator.IsViewable(typeof(UserQueryEntity)))
                return null;

            return UserQueryMenuItemConstructor.Construct(seachControl);
        }

        internal static UserQueryEntity FromSearchControl(SearchControl searchControl)
        {
            QueryDescription description = DynamicQueryServer.GetQueryDescription(searchControl.QueryName);

            return searchControl.GetQueryRequest(true).ToUserQuery(description, 
                QueryClient.GetQuery(searchControl.QueryName), 
                FindOptions.DefaultPagination);
        }

        internal static void ToSearchControl(UserQueryEntity uq, SearchControl searchControl)
        {
            var filters = uq.AppendFilters ? searchControl.FilterOptions.ToList() :
                 searchControl.FilterOptions.Where(f => f.Frozen).Concat(uq.Filters.Select(qf => new FilterOption
             {
                 ColumnName = qf.Token.Token.FullKey(),
                 Operation = qf.Operation.Value,
                 Value = Signum.Entities.UserAssets.FilterValueConverter.Parse(qf.ValueString, qf.Token.Token.Type, isList: qf.Operation.Value.IsList())
             })).ToList();

            var columns = uq.Columns.Select(qc => new ColumnOption
            {
                ColumnName = qc.Token.Token.FullKey(),
                DisplayName = qc.DisplayName.DefaultText(null)
            }).ToList();

            var orders = uq.Orders.Select(of => new OrderOption
            {
                ColumnName = of.Token.Token.FullKey(),
                OrderType = of.OrderType,
            }).ToList();

            var pagination = uq.GetPagination() ?? Finder.GetQuerySettings(searchControl.QueryName).Pagination ?? FindOptions.DefaultPagination;

            searchControl.Reinitialize(filters, columns, uq.ColumnsMode, orders, pagination);
        }

        internal static void ToCountSearchControl(UserQueryEntity uq, CountSearchControl countSearchControl)
        {
            var filters = uq.AppendFilters ? countSearchControl.FilterOptions.ToList() :
                countSearchControl.FilterOptions.Where(f => f.Frozen).Concat(uq.Filters.Select(qf => new FilterOption
                {
                    ColumnName = qf.Token.Token.FullKey(),
                    Operation = qf.Operation.Value,
                    Value = Signum.Entities.UserAssets.FilterValueConverter.Parse(qf.ValueString, qf.Token.Token.Type, isList: qf.Operation.Value.IsList())
                })).ToList();

            var columns = uq.Columns.Select(qc => new ColumnOption
            {

                ColumnName = qc.Token.Token.FullKey(),
                DisplayName = qc.DisplayName.DefaultText(null)
            }).ToList();

            var orders = uq.Orders.Select(of => new OrderOption
            {
                ColumnName = of.Token.Token.FullKey(),
                OrderType = of.OrderType,
            }).ToList();

            countSearchControl.Reinitialize(filters, columns, uq.ColumnsMode, orders);
            countSearchControl.Text = uq.DisplayName + ": {0}";
            countSearchControl.LinkClick += (object sender, EventArgs e) =>
            {
                Finder.Explore(new ExploreOptions(countSearchControl.QueryName)
                {
                    InitializeSearchControl = sc => UserQueryClient.SetUserQuery(sc, uq)
                });
            };
        }

        internal static void Explore(UserQueryEntity userQuery, Entity currentEntity)
        {
            var query = QueryClient.GetQueryName(userQuery.Query.Key);

            Finder.Explore(new ExploreOptions(query)
            {
                InitializeSearchControl = sc =>
                {
                    using (userQuery.EntityType == null ? null : CurrentEntityConverter.SetCurrentEntity(currentEntity))
                        UserQueryClient.SetUserQuery(sc, userQuery);
                }
            });
        }
    }
}
