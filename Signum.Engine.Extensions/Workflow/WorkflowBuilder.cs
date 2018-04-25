﻿using Signum.Entities.Workflow;
using Signum.Engine;
using Signum.Engine.Operations;
using Signum.Entities;
using Signum.Entities.Reflection;
using Signum.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Signum.Utilities.DataStructures;
using System.Linq.Expressions;
using System.Xml;

namespace Signum.Engine.Workflow
{
    public partial class WorkflowBuilder
    {
        public static readonly XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        public static readonly XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
        public static readonly XNamespace bpmndi = "http://www.omg.org/spec/BPMN/20100524/DI";
        public static readonly XNamespace dc = "http://www.omg.org/spec/DD/20100524/DC";
        public static readonly XNamespace di = "http://www.omg.org/spec/DD/20100524/DI";
        public static readonly string targetNamespace = "http://bpmn.io/schema/bpmn";

        private Dictionary<Lite<WorkflowPoolEntity>, PoolBuilder> pools;
        private List<XmlEntity<WorkflowConnectionEntity>> messageFlows; //Contains the connections that cross two different Pools EXCLUDING the connections internal to each pool
        private WorkflowEntity workflow;

        public object HasMultipleInputsAndOutputsAtTheSameTime { get; private set; }

        public WorkflowBuilder(WorkflowEntity wf)
        {
            using (HeavyProfiler.Log("WorkflowBuilder"))
            using (new EntityCache())
            {
                this.workflow = wf;

                List<WorkflowConnectionEntity> connections;
                List<WorkflowEventEntity> events;
                List<WorkflowActivityEntity> activities;
                List<WorkflowGatewayEntity> gateways;

                if (wf.IsNew)
                {
                    connections = new List<WorkflowConnectionEntity>();
                    events = new List<WorkflowEventEntity>();
                    activities = new List<WorkflowActivityEntity>();
                    gateways = new List<WorkflowGatewayEntity>();
                }
                else
                {
                    connections = wf.WorkflowConnections().ToList();
                    events = wf.WorkflowEvents().ToList();
                    activities = wf.WorkflowActivities().ToList();
                    gateways = wf.WorkflowGateways().ToList();
                }

                var xmlConnections = connections.Select(a => new XmlEntity<WorkflowConnectionEntity>(a)).ToList();
                var nodes = events.Cast<IWorkflowNodeEntity>().Concat(activities).Concat(gateways).ToList();

                this.pools = (from n in nodes
                              group n by n.Lane into grLane
                              select new LaneBuilder(grLane.Key,
                              grLane.OfType<WorkflowActivityEntity>(),
                              grLane.OfType<WorkflowEventEntity>(),
                              grLane.OfType<WorkflowGatewayEntity>(),
                              xmlConnections.Where(c => c.Entity.From.Lane.Is(grLane.Key) || c.Entity.To.Lane.Is(grLane.Key))) into lb
                              group lb by lb.lane.Entity.Pool into grPool
                              select new PoolBuilder(grPool.Key, grPool, xmlConnections.Where(c => c.Entity.From.Lane.Pool.Is(grPool.Key) && c.Entity.To.Lane.Pool.Is(grPool.Key))))
                             .ToDictionary(pb => pb.pool.Entity.ToLite());

                this.messageFlows = xmlConnections.Where(c => !c.Entity.From.Lane.Pool.Is(c.Entity.To.Lane.Pool)).ToList();

            }
        }

        internal WorkflowModel GetWorkflowModel()
        {
            XDocument xml = GetXDocument();

            Dictionary<string, ModelEntity> dic = new Dictionary<string, ModelEntity>();

            dic.AddRange(this.pools.Values.Select(pb => pb.pool.ToModelKVP()));

            var lanes = this.pools.Values.SelectMany(pb => pb.GetLanes());
            dic.AddRange(lanes.Select(lb => lb.lane.ToModelKVP()));

            dic.AddRange(lanes.SelectMany(lb => lb.GetActivities()).Select(a => a.ToModelKVP()));

            // Only Start events because end event has no model and extra properties for now
            dic.AddRange(lanes.SelectMany(lb => lb.GetEvents().Where(e => e.Entity.Type.IsStart())).Select(a => a.ToModelKVP()));

            dic.AddRange(this.messageFlows.Select(mf => mf.ToModelKVP()));
            dic.AddRange(this.pools.Values.SelectMany(pb => pb.GetSequenceFlows()).Select(sf => sf.ToModelKVP()));

            return new WorkflowModel
            {
                DiagramXml = xml.ToString(),
                Entities = dic.Select(kvp => new BpmnEntityPairEmbedded { BpmnElementId = kvp.Key, Model = kvp.Value }).ToMList()
            };
        }

        public static XDocument ParseDocument(string diagramXml)
        {
            XmlNamespaceManager nmgr = new XmlNamespaceManager(new NameTable());
            nmgr.AddNamespace("xsi", WorkflowBuilder.xsi.NamespaceName);
            nmgr.AddNamespace("bpmn", WorkflowBuilder.bpmn.NamespaceName);
            nmgr.AddNamespace("bpmndi", WorkflowBuilder.bpmndi.NamespaceName);
            nmgr.AddNamespace("dc", WorkflowBuilder.dc.NamespaceName);
            nmgr.AddNamespace("di", WorkflowBuilder.di.NamespaceName);

            XmlParserContext pctx = new XmlParserContext(null, nmgr, null, XmlSpace.None);
            XmlTextReader reader = new XmlTextReader(diagramXml, XmlNodeType.Document, pctx);

            return XDocument.Load(reader);
        }

        public XDocument GetXDocument()
        {
            return new XDocument(
              new XDeclaration("1.0", "UTF-8", null),
                  new XElement(bpmn + "definitions",
                      new XAttribute(XNamespace.Xmlns + "bpmn", bpmn.NamespaceName),
                      new XAttribute(XNamespace.Xmlns + "bpmndi", bpmndi.NamespaceName),
                      new XAttribute(XNamespace.Xmlns + "dc", dc.NamespaceName),
                      new XAttribute(XNamespace.Xmlns + "di", di.NamespaceName),
                      new XAttribute("targetNamespace", targetNamespace),
                      new XElement(bpmn + "collaboration",
                        new XAttribute("id", "Collaboration_" + workflow.Id),
                        pools.Values.Select(a => a.GetParticipantElement()).ToList(),
                        GetMessageFlowElements()),
                      GetProcesses(),
                      new XElement(bpmndi + "BPMNDiagram",
                          new XAttribute("id", "BPMNDiagram" + workflow.Id),
                          new XElement(bpmndi + "BPMNPlane",
                              new XAttribute("id", "BPMNPlane_" + workflow.Id),
                              new XAttribute("bpmnElement", "Collaboration_" + workflow.Id),
                              GetDiagramElements()))));
        }

        internal List<XElement> GetMessageFlowElements()
        {
            return messageFlows.Select(a =>
                new XElement(bpmn + "messageFlow",
                    new XAttribute("id", a.bpmnElementId),
                    a.Entity.Name.HasText() ? new XAttribute("name", a.Entity.Name) : null,
                    new XAttribute("sourceRef", GetBpmnElementId(a.Entity.From)),
                    new XAttribute("targetRef", GetBpmnElementId(a.Entity.To))
                )
            ).ToList();
        }
        
        internal List<XElement> GetProcesses()
        {
            return pools.Values.Select(a => a.GetProcessElement()).ToList();
        }

        internal List<XElement> GetDiagramElements()
        {
            List<XElement> res = new List<XElement>();
            res.AddRange(pools.Values.SelectMany(a => a.GetDiagramElements()).ToList());
            res.AddRange(messageFlows.Select(a => a.Element));
            return res;
        }

        public void ApplyChanges(WorkflowModel model, WorkflowReplacementModel replacements)
        {
            var document =  WorkflowBuilder.ParseDocument(model.DiagramXml);

            var participants = document.Descendants(bpmn + "collaboration").Elements(bpmn + "participant").ToDictionaryEx(a => a.Attribute("id").Value);
            var processElements = document.Descendants(bpmn + "process").ToDictionaryEx(a => a.Attribute("id").Value);
            var diagramElements = document.Descendants(bpmndi + "BPMNPlane").Elements().ToDictionaryEx(a => a.Attribute("bpmnElement").Value, "bpmnElement");

            if (participants.Count != processElements.Count)
                throw new InvalidOperationException(WorkflowValidationMessage.ParticipantsAndProcessesAreNotSynchronized.NiceToString());

            Locator locator = new Workflow.Locator(this, diagramElements, model, replacements);
            var oldPools = this.pools.Values.ToDictionaryEx(a => a.pool.bpmnElementId, "pools");

            Synchronizer.Synchronize(participants, oldPools,
                (id, pa) =>
                {
                    var wp = new WorkflowPoolEntity { Xml = new WorkflowXmlEmbedded(), Workflow = this.workflow }.ApplyXml(pa, locator);
                    var pb = new PoolBuilder(wp, Enumerable.Empty<LaneBuilder>(), Enumerable.Empty<XmlEntity<WorkflowConnectionEntity>>());
                    this.pools.Add(wp.ToLite(), pb);
                    pb.ApplyChanges(processElements.GetOrThrow(pa.Attribute("processRef").Value), locator);
                },
                (id, pb) =>
                {
                    this.pools.Remove(pb.pool.Entity.ToLite());
                    pb.DeleteAll(locator);
                },
                (id, pa, pb) =>
                {
                    var wp = pb.pool.Entity.ApplyXml(pa, locator);
                    pb.ApplyChanges(processElements.GetOrThrow(pa.Attribute("processRef").Value), locator);
                });

            var messageFlows = document.Descendants(bpmn + "collaboration").Elements(bpmn + "messageFlow").ToDictionaryEx(a => a.Attribute("id").Value);
            var oldMessageFlows = this.messageFlows.ToDictionaryEx(a => a.bpmnElementId, "messageFlows");

            Synchronizer.Synchronize(messageFlows, oldMessageFlows,
                (id, mf) =>
                {
                    var wc = new WorkflowConnectionEntity { Xml = new WorkflowXmlEmbedded() }.ApplyXml(mf, locator);
                    this.messageFlows.Add(new XmlEntity<WorkflowConnectionEntity>(wc));
                },
                (id, omf) =>
                {
                    this.messageFlows.Remove(omf);
                    omf.Entity.Delete(WorkflowConnectionOperation.Delete);
                },
                (id, mf, omf) =>
                {
                    omf.Entity.ApplyXml(mf, locator);
                });
        }

        internal IWorkflowNodeEntity FindEntity(string bpmElementId)
        {
            return this.pools.Values.Select(pb => pb.FindEntity(bpmElementId)).NotNull().SingleOrDefaultEx();
        }

        internal LaneBuilder FindLane(WorkflowLaneEntity lane)
        {
            return this.pools.Values.SelectMany(a => a.GetLanes()).Single(l => l.lane.Entity.Is(lane));
        }

        private string GetBpmnElementId(IWorkflowNodeEntity node)
        {
            return this.pools.GetOrThrow(node.Lane.Pool.ToLite()).GetLaneBuilder(node.Lane.ToLite()).GetBpmnElementId(node);
        }

        public PreviewResult PreviewChanges(XDocument document, WorkflowModel model)
        {
            var oldTasks = this.pools.Values.SelectMany(p => p.GetAllActivities())
                .ToDictionary(a => a.bpmnElementId);

            var newElements = document.Descendants().Where(a => LaneBuilder.WorkflowActivityTypes.Values.Contains(a.Name.LocalName))
                .ToDictionary(a => a.Attribute("id").Value);

            var entities = model.Entities.ToDictionaryEx(a => a.BpmnElementId);

            return new PreviewResult
            {
                Model = new WorkflowReplacementModel
                {
                    Replacements = oldTasks.Where(kvp => !newElements.ContainsKey(kvp.Key) && kvp.Value.Entity.CaseActivities().Any())
                    .Select(a => new WorkflowReplacementItemEmbedded
                    {
                        OldTask = a.Value.Entity.ToLite(),
                        SubWorkflow = a.Value.Entity.SubWorkflow?.Workflow.ToLite()
                    })
                    .ToMList(),

                },
                NewTasks = newElements.Select(kvp => new PreviewTask
                {
                    BpmnId = kvp.Key,
                    Name = kvp.Value.Attribute("name")?.Value,
                    SubWorkflow = ((WorkflowActivityModel)entities.GetOrThrow(kvp.Key).Model).SubWorkflow?.Workflow.ToLite()
                }).ToList(),
            };
        }

        internal WorkflowEntity Clone()
        {
            var newName = 0.To(1000).Select(i => $"Copy{(i == 0 ? "" : $" ({i})")} of {this.workflow.Name}").FirstEx(s => !Database.Query<WorkflowEntity>().Any(w => w.Name == s));

            WorkflowEntity newWorkflow = new WorkflowEntity
            {
                Name = newName,
                MainEntityType = this.workflow.MainEntityType,
            }.Save();

            Dictionary<IWorkflowNodeEntity, IWorkflowNodeEntity> nodes = new Dictionary<IWorkflowNodeEntity, IWorkflowNodeEntity>();

            using (OperationLogic.AllowSave<WorkflowPoolEntity>())
            using (OperationLogic.AllowSave<WorkflowLaneEntity>())
            using (OperationLogic.AllowSave<WorkflowActivityEntity>())
            using (OperationLogic.AllowSave<WorkflowGatewayEntity>())
            using (OperationLogic.AllowSave<WorkflowEventEntity>())
            {
                foreach (var pb in this.pools.Values)
                {
                    pb.Clone(newWorkflow, nodes);
                }
            }

            using (OperationLogic.AllowSave<WorkflowConnectionEntity>())
            {
                var allConnections = GetAllConnections().ToDictionaryEx(a => a.bpmnElementId);
                allConnections.Values.Select(c => new WorkflowConnectionEntity
                {
                    Name = c.Entity.Name,
                    BpmnElementId = c.bpmnElementId,
                    Action = c.Entity.Action,
                    Condition = c.Entity.Condition,
                    DecisonResult = c.Entity.DecisonResult,
                    Order = c.Entity.Order,
                    Xml = c.Entity.Xml,
                    From = nodes.GetOrThrow(c.Entity.From),
                    To = nodes.GetOrThrow(c.Entity.To),
                }).SaveList();
            }

            
            foreach (var item in nodes.Where(a => a.Key is WorkflowEventEntity e && e.Type.IsTimerStart()))
            {
                WorkflowEventTaskLogic.CloneScheduledTasks((WorkflowEventEntity)item.Key, (WorkflowEventEntity)item.Value);
            }


            WorkflowBuilder wb = new WorkflowBuilder(newWorkflow);
            newWorkflow.FullDiagramXml = new WorkflowXmlEmbedded { DiagramXml = wb.GetXDocument().ToString() };
            newWorkflow.Save();

            return newWorkflow;
        }

        public void Delete() {
            this.pools.SingleEx().Value.DeleteAll(null);
            this.workflow.Cases().UnsafeDelete();
            this.workflow.Delete();
        }

        private void DeleteCases(Expression<Func<CaseEntity, bool>> filter)
        {
            this.pools.SingleEx().Value.DeleteCaseActivities(filter);
            Database.Query<CaseEntity>()
                .Where(c => c.Workflow.Is(this.workflow) && filter.Evaluate(c))
                .UnsafeDelete();
        }

        private IEnumerable<XmlEntity<WorkflowConnectionEntity>> GetAllConnections()
        {
            return this.messageFlows.Concat(this.pools.Values.SelectMany(p => p.GetSequenceFlows()));
        }

        internal void ValidateGraph()
        {
            WorkflowNodeGraph wg = new WorkflowNodeGraph
            {
                Workflow = this.workflow,
                Activities = this.pools.SelectMany(p => p.Value.GetLanes().SelectMany(l => l.GetActivities())).Select(a => a.Entity).ToDictionary(a => a.ToLite()),
                Events = this.pools.SelectMany(p => p.Value.GetLanes().SelectMany(l => l.GetEvents())).Select(a => a.Entity).ToDictionary(a => a.ToLite()),
                Gateways = this.pools.SelectMany(p => p.Value.GetLanes().SelectMany(l => l.GetGateways())).Select(a => a.Entity).ToDictionary(a => a.ToLite()),
                Connections = this.GetAllConnections().Select(a => a.Entity).ToDictionary(a => a.ToLite()),
            };
            
            wg.FillGraphs();
            var errors = wg.Validate((g, newDirection) =>
            {
                g.Direction = newDirection;
                g.Execute(WorkflowGatewayOperation.Save);
            });

            var error =  errors.ToString("\r\n").DefaultText(null);
            if (error != null)
                throw new ApplicationException(error);
        }
    }

    public class PreviewResult
    {
        public WorkflowReplacementModel Model;
        public List<PreviewTask> NewTasks;
    }

    public class PreviewTask
    {
        public string BpmnId;
        public string Name;
        public Lite<WorkflowEntity> SubWorkflow;
    }

    public class Locator
    {
        WorkflowBuilder wb;
        Dictionary<string, XElement> diagramElements;
        Dictionary<string, ModelEntity> entitiesFromModel;

        public Locator(WorkflowBuilder wb, Dictionary<string, XElement> diagramElements, WorkflowModel model, WorkflowReplacementModel replacements)
        {
            this.wb = wb;
            this.diagramElements = diagramElements;
            this.Replacements = (replacements?.Replacements).EmptyIfNull().ToDictionary(a => a.OldTask, a => a.NewTask);
            this.entitiesFromModel = model.Entities.ToDictionary(a => a.BpmnElementId, a => a.Model);
        }

        public IWorkflowNodeEntity FindEntity(string bpmElementId)
        {
            return wb.FindEntity(bpmElementId);
        }

        internal WorkflowBuilder.LaneBuilder FindLane(WorkflowLaneEntity lane)
        {
            return wb.FindLane(lane);
        }

        public bool ExistDiagram(string bpmElementId)
        {
            return diagramElements.ContainsKey(bpmElementId);
        }

        public XElement GetDiagram(string bpmElementId)
        {
            return diagramElements.GetOrThrow(bpmElementId);
        }


        public Dictionary<Lite<WorkflowActivityEntity>, string> Replacements; 
        public WorkflowActivityEntity GetReplacement(Lite<WorkflowActivityEntity> lite)
        {
            string bpmnElementId = Replacements.GetOrThrow(lite);
            return (WorkflowActivityEntity)this.FindEntity(bpmnElementId);
        }

        internal T GetModelEntity<T>(string bpmnElementId)
            where T : ModelEntity, new()
        {
            return (T)this.entitiesFromModel.TryGetC(bpmnElementId);
        }

        internal bool HasReplacement(Lite<WorkflowActivityEntity> lite)
        {
            return Replacements.GetOrThrow(lite).HasText();
        }
    }

    public static class NodeEntityExtensions
    {
        public static WorkflowPoolEntity ApplyXml(this WorkflowPoolEntity wp, XElement participant, Locator locator)
        {
            var bpmnElementId = participant.Attribute("id").Value;
            var model = locator.GetModelEntity<WorkflowPoolModel>(bpmnElementId);
            if (model != null)
                wp.SetModel(model);
            wp.BpmnElementId = bpmnElementId;
            wp.Name = participant.Attribute("name").Value;
            wp.Xml.DiagramXml = locator.GetDiagram(bpmnElementId).ToString();
            if (GraphExplorer.HasChanges(wp))
                wp.Execute(WorkflowPoolOperation.Save);
            return wp;
        }

        public static WorkflowLaneEntity ApplyXml(this WorkflowLaneEntity wl, XElement lane, Locator locator)
        {
            var bpmnElementId = lane.Attribute("id").Value;
            var model = locator.GetModelEntity<WorkflowLaneModel>(bpmnElementId);
            if (model != null)
                wl.SetModel(model);
            wl.BpmnElementId = bpmnElementId;
            wl.Name = lane.Attribute("name").Value;
            wl.Xml.DiagramXml = locator.GetDiagram(bpmnElementId).ToString();
            if (GraphExplorer.HasChanges(wl))
                wl.Execute(WorkflowLaneOperation.Save);

            return wl;
        }

        public static WorkflowEventEntity ApplyXml(this WorkflowEventEntity we, XElement @event, Locator locator)
        {
            var bpmnElementId = @event.Attribute("id").Value;
            we.BpmnElementId = bpmnElementId;
            var model = locator.GetModelEntity<WorkflowEventModel>(bpmnElementId);
            if (model != null)
                we.SetModel(model);
            else
            {
                we.Name = @event.Attribute("name")?.Value;
                we.Type = WorkflowBuilder.LaneBuilder.WorkflowEventTypes.First(kvp => kvp.Value == @event.Name.LocalName).Key;
            }

            we.Xml.DiagramXml = locator.GetDiagram(bpmnElementId).ToString();

            if (GraphExplorer.HasChanges(we))
                we.Execute(WorkflowEventOperation.Save);

            if (model != null)
                WorkflowEventTaskModel.ApplyModel(we, model.Task);

            return we;
        }

        public static WorkflowActivityEntity ApplyXml(this WorkflowActivityEntity wa, XElement activity, Locator locator)
        {
            var bpmnElementId = activity.Attribute("id").Value;
            var model = locator.GetModelEntity<WorkflowActivityModel>(bpmnElementId);
            if (model != null)
                wa.SetModel(model);
            wa.BpmnElementId = bpmnElementId;
            wa.Name = activity.Attribute("name")?.Value ?? bpmnElementId;
            wa.Xml.DiagramXml = locator.GetDiagram(bpmnElementId).ToString();
            if (GraphExplorer.HasChanges(wa))
                wa.Execute(WorkflowActivityOperation.Save);

            return wa;
        }

        public static WorkflowGatewayEntity ApplyXml(this WorkflowGatewayEntity wg, XElement gateway, Locator locator)
        {
            var bpmnElementId = gateway.Attribute("id").Value;
            wg.BpmnElementId = bpmnElementId;
            wg.Name = gateway.Attribute("name")?.Value;
            wg.Type = WorkflowBuilder.LaneBuilder.WorkflowGatewayTypes.Single(kvp => kvp.Value == gateway.Name.LocalName).Key;
            wg.Xml.DiagramXml = locator.GetDiagram(bpmnElementId).ToString();
            if (GraphExplorer.HasChanges(wg))
                wg.Execute(WorkflowGatewayOperation.Save);

            return wg;
        }

        public static WorkflowConnectionEntity ApplyXml(this WorkflowConnectionEntity wc, XElement flow, Locator locator)
        {
            wc.From = locator.FindEntity(flow.Attribute("sourceRef").Value);
            wc.To = locator.FindEntity(flow.Attribute("targetRef").Value);

            var bpmnElementId = flow.Attribute("id").Value;
            var model = locator.GetModelEntity<WorkflowConnectionModel>(bpmnElementId);
            if (model != null)
                wc.SetModel(model);
            wc.BpmnElementId = bpmnElementId;

            var name = flow.Attribute("name")?.Value;
            name = (name.TryBeforeLast(":") ?? name);

            if (model != null && model.Order.HasValue)
                name = name + ": " + model.Order.ToString();

            wc.Name = name;
            wc.Xml.DiagramXml = locator.GetDiagram(bpmnElementId).ToString();

            var gateway = (wc.From as WorkflowGatewayEntity);

            if (gateway == null || gateway.Type != WorkflowGatewayType.Exclusive)
            {
                wc.DecisonResult = null;
                wc.Order = null;
            };

            if (gateway == null || gateway.Type == WorkflowGatewayType.Parallel)
                wc.Condition = null;

            if (GraphExplorer.HasChanges(wc))
                wc.Execute(WorkflowConnectionOperation.Save);
            return wc;
        }
    }

    public class XmlEntity<T>
    where T : Entity, IWorkflowObjectEntity, IWithModel
    {
        public XmlEntity(T entity)
        {
            var finalXml = @"<bpmn:definitions xmlns:xsi = " + ToQuoted(WorkflowBuilder.xsi.ToString()) + " " +
                          @"xmlns:bpmn = " + ToQuoted(WorkflowBuilder.bpmn.ToString()) + " " +
                          @"xmlns:bpmndi = " + ToQuoted(WorkflowBuilder.bpmndi.ToString()) + " " +
                          @"xmlns:dc = " + ToQuoted(WorkflowBuilder.dc.ToString()) + " " +
                          @"xmlns:di = " + ToQuoted(WorkflowBuilder.di.ToString()) + @" id = ""Definitions_1"" targetNamespace = " + ToQuoted(WorkflowBuilder.targetNamespace) + " > " +
                          @"<bpmndi:BPMNDiagram id = ""BPMNDiagram_1"" >"
                           + entity.Xml.DiagramXml +
                          @"</bpmndi:BPMNDiagram>" +
                          @"</bpmn:definitions>";

            Entity = entity;
            Document = XDocument.Parse(finalXml);
            Element = Document.Root.Element(WorkflowBuilder.bpmndi + "BPMNDiagram").Elements().First();
            bpmnElementId = Element.Attribute("bpmnElement").Value;
        }

        public XDocument Document;

        private XElement element;
        public XElement Element { get { return element; } private set { element = value; } }
        public string bpmnElementId;
        
        public T Entity;

        public KeyValuePair<string, Entity> ToKVP() => new KeyValuePair<string, Entity>(bpmnElementId, Entity);
        public KeyValuePair<string, ModelEntity> ToModelKVP() => new KeyValuePair<string, ModelEntity>(bpmnElementId, Entity.GetModel());

        public override string ToString() => $"{bpmnElementId} {Entity.GetType().Name} {Entity.Name}";

        public string ToQuoted(string str)
        {
            return "\"" + str + "\"";
        }
    }

}
