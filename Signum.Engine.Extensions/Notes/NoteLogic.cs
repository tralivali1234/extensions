﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Engine.Maps;
using Signum.Entities;
using Signum.Engine.Authorization;
using Signum.Engine;
using Signum.Utilities;
using Signum.Engine.DynamicQuery;
using Signum.Entities.Authorization;
using System.Reflection;
using Signum.Utilities.ExpressionTrees;
using Signum.Entities.Basics;
using Signum.Engine.Operations;
using System.Linq.Expressions;
using Signum.Utilities.Reflection;
using Signum.Entities.Notes;
using Signum.Engine.Extensions.Basics;
using Signum.Engine.Basics;

namespace Signum.Engine.Notes
{
    public static class NoteLogic
    {
        static Expression<Func<Entity, IQueryable<NoteEntity>>> NotesExpression =
            ident => Database.Query<NoteEntity>().Where(n => n.Target.RefersTo(ident));
        [ExpressionField]
        public static IQueryable<NoteEntity> Notes(this Entity ident)
        {
            return NotesExpression.Evaluate(ident);
        }

        static HashSet<NoteTypeEntity> SystemNoteTypes = new HashSet<NoteTypeEntity>();
        static bool started = false;

        public static void Start(SchemaBuilder sb, DynamicQueryManager dqm, params Type[] registerExpressionsFor)
        {
            if (sb.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                sb.Include<NoteEntity>()
                    .WithSave(NoteOperation.Save)
                    .WithQuery(dqm, () => n => new
                    {
                        Entity = n,
                        n.Id,
                        n.CreatedBy,
                        n.CreationDate,
                        n.Title,
                        Text = n.Text.Etc(100),
                        n.Target
                    });
                
                new Graph<NoteEntity>.ConstructFrom<Entity>(NoteOperation.CreateNoteFromEntity)
                {
                    Construct = (a, _) => new NoteEntity{ CreationDate = TimeZoneManager.Now, Target = a.ToLite() }
                }.Register();

                sb.Include<NoteTypeEntity>()
                    .WithSave(NoteTypeOperation.Save)
                    .WithQuery(dqm, () => t => new
                    {
                        Entity = t,
                        t.Id,
                        t.Name,
                        t.Key,
                    });
                
                SemiSymbolLogic<NoteTypeEntity>.Start(sb, () => SystemNoteTypes);

                if (registerExpressionsFor != null)
                {
                    var exp = Signum.Utilities.ExpressionTrees.Linq.Expr((Entity ident) => ident.Notes());
                    foreach (var type in registerExpressionsFor)
                        dqm.RegisterExpression(new ExtensionInfo(type, exp, exp.Body.Type, "Notes", () => typeof(NoteEntity).NicePluralName()));
                }

                started = true;
            }
        }

        public static void RegisterNoteType(NoteTypeEntity noteType)
        {
            if (!noteType.Key.HasText())
                throw new InvalidOperationException("noteType must have a key, use MakeSymbol method after the constructor when declaring it");

            SystemNoteTypes.Add(noteType);
        }


        public static NoteEntity CreateNote<T>(this Lite<T> entity, string text, NoteTypeEntity noteType,  Lite<UserEntity> user = null, string title = null) where T : class, IEntity
        {
            if (started == false)
                return null;

            return new NoteEntity
            {               
                CreatedBy = user ?? UserEntity.Current.ToLite(),
                Text = text,
                Title = title,
                Target = (Lite<Entity>)Lite.Create(entity.EntityType, entity.Id, entity.ToString()),
                NoteType = noteType
            }.Execute(NoteOperation.Save);
        }

        public static void RegisterUserTypeCondition(SchemaBuilder sb, TypeConditionSymbol typeCondition)
        {
            sb.Schema.Settings.AssertImplementedBy((NoteEntity uq) => uq.CreatedBy, typeof(UserEntity));

            TypeConditionLogic.RegisterCompile<NoteEntity>(typeCondition,
                uq => uq.CreatedBy.RefersTo(UserEntity.Current));
        }
    }
}
