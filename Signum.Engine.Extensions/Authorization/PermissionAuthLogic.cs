﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Engine.Maps;
using Signum.Entities.Authorization;
using Signum.Entities.Basics;
using Signum.Engine.DynamicQuery;
using Signum.Engine.Basics;
using Signum.Utilities;
using Signum.Utilities.DataStructures;
using System.Threading;
using Signum.Entities;
using System.Reflection;
using Signum.Utilities.Reflection;
using System.Xml.Linq;

namespace Signum.Engine.Authorization
{

    public static class PermissionAuthLogic
    {
        static List<Enum> permissions = new List<Enum>();
        public static void RegisterPermissions(params Enum[] type)
        {
            permissions.AddRange(type.NotNull()); 
        }

        public static void RegisterTypes(params Type[] types)
        {
            foreach (var t in types.NotNull())
            {
                if (!t.IsEnum)
                    throw new ArgumentException("{0} is not an Enum".Formato(t.Name));

                permissions.AddRange(Enum.GetValues(t).Cast<Enum>()); 
            }
        }

        public static IEnumerable<Enum> RegisteredPermission
        {
            get { return permissions; }
        }

        static AuthCache<RulePermissionDN, PermissionAllowedRule, PermissionDN, Enum, bool> cache;

        public static IManualAuth<Enum, bool> Manual { get { return cache; } }

        public static bool IsStarted { get { return cache != null; } }

        public static void AssertStarted(SchemaBuilder sb)
        {
            sb.AssertDefined(ReflectionTools.GetMethodInfo(() => Start(null)));
        }

        public static void Start(SchemaBuilder sb)
        {
            if (sb.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                AuthLogic.AssertStarted(sb);

                sb.Include<PermissionDN>();

                MultiEnumLogic<PermissionDN>.Start(sb, () => RegisteredPermission.ToHashSet());

                cache = new AuthCache<RulePermissionDN, PermissionAllowedRule, PermissionDN, Enum, bool>(sb,
                    MultiEnumLogic<PermissionDN>.ToEnum,
                    MultiEnumLogic<PermissionDN>.ToEntity,
                    merger: new PermissionMerger(),
                    invalidateWithTypes: false);

                RegisterPermissions(BasicPermission.AdminRules);

                AuthLogic.ExportToXml += () => cache.ExportXml("Permissions", "Permission", PermissionDN.UniqueKey, b => b.ToString());
                AuthLogic.ImportFromXml += (x, roles, replacements) =>
                {
                    string replacementKey = typeof(PermissionDN).Name;

                    replacements.AskForReplacements(
                        x.Element("Permissions").Elements("Role").SelectMany(r => r.Elements("Permission")).Select(p => p.Attribute("Resource").Value).ToHashSet(),
                        MultiEnumLogic<PermissionDN>.AllUniqueKeys().ToHashSet(),
                        replacementKey);

                    return cache.ImportXml(x, "Permissions", "Permission", roles,
                        s => MultiEnumLogic<PermissionDN>.TryToEntity(replacements.Apply(replacementKey, s)), bool.Parse);
                };
            }
        }
 
        public static void Authorize(this Enum permissionKey)
        {
            if (!IsAuthorized(permissionKey))
                throw new UnauthorizedAccessException("Permission '{0}' is denied".Formato(permissionKey));
        }

        public static string IsAuthorizedString(this Enum permissionKey)
        {
            if (!IsAuthorized(permissionKey))
                return "Permission '{0}' is denied".Formato(permissionKey);

            return null;
        }

        public static bool IsAuthorized(this Enum permissionKey)
        {
            if (!AuthLogic.IsEnabled || ExecutionMode.InGlobal || cache == null)
                return true;

            return cache.GetAllowed(RoleDN.Current.ToLite(), permissionKey);
        }

        public static bool IsAuthorized(this Enum permissionKey, Lite<RoleDN> role)
        {
            return cache.GetAllowed(role, permissionKey);
        }

        public static DefaultDictionary<Enum, bool> ServicePermissionRules()
        {
            return cache.GetDefaultDictionary();
        }

        public static PermissionRulePack GetPermissionRules(Lite<RoleDN> roleLite)
        {
            var result = new PermissionRulePack { Role = roleLite };
            cache.GetRules(result, MultiEnumLogic<PermissionDN>.AllEntities());
            return result;
        }

        public static void SetPermissionRules(PermissionRulePack rules)
        {
            cache.SetRules(rules, r => true);
        }
    }



    class PermissionMerger : Merger<Enum, bool>
    {
        protected override bool Union(Enum key, Lite<RoleDN> role, IEnumerable<bool> baseValues)
        {
            return Max(baseValues);
        }

        static bool Max(IEnumerable<bool> baseValues)
        {
            return baseValues.Any(a => a);
        }

        protected override bool Intersection(Enum key, Lite<RoleDN> role, IEnumerable<bool> baseValues)
        {
            return Min(baseValues);
        }

        static bool Min(IEnumerable<bool> baseValues)
        {
            return baseValues.All(a => a);
        }

        public override Func<Enum, bool> MergeDefault(Lite<RoleDN> role, IEnumerable<Func<Enum, bool>> baseDefaultValues)
        {
            return new ConstantFunction<Enum, bool>(AuthLogic.GetDefaultAllowed(role)).GetValue;
        }
    }
}
