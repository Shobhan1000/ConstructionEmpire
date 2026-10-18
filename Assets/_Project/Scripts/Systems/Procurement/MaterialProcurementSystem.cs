using System;
using System.Collections.Generic;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Procurement;
using ConstructionEmpire.Systems.Finance;
using ConstructionEmpire.Systems.Randomness;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Procurement
{
    public readonly struct ProcurementQuote
    {
        public double UnitPrice {get;} public double Discount {get;} public double Total {get;}public int LeadHours {get;}
        public ProcurementQuote(double price,double discount,double total,int hours) {UnitPrice=price;Discount=discount;Total=total;LeadHours=hours;}
    }
    public sealed class MaterialProcurementSystem : IDisposable
    {
        public double ResearchDiscountFraction {get;set;}
        private readonly SupplierSystem suppliers;private readonly MaterialInventoryData inventory;private readonly IMaterialCostSink finance;private readonly ScheduledEventQueue scheduler;
        private readonly GameTimeManager clock;private readonly SeededRandom random;private readonly ProcurementSettings settings;private readonly EventSubscriptions subscriptions=new EventSubscriptions();
        private readonly List<ProcurementOrder> orders=new List<ProcurementOrder>();private readonly List<long> scheduled=new List<long>();private bool disposed;
        private readonly struct DeliveryDue {public readonly MaterialProcurementSystem Owner;public readonly ProcurementOrder Order;public DeliveryDue(MaterialProcurementSystem owner,ProcurementOrder order) {Owner=owner;Order=order;} }
        public IReadOnlyList<ProcurementOrder> Orders=>orders.AsReadOnly();
        public MaterialProcurementSystem(SupplierSystem suppliers,MaterialInventoryData inventory,IMaterialCostSink finance,ScheduledEventQueue scheduler,GameTimeManager clock,SeededRandom random,ProcurementSettings settings)
        {this.suppliers=suppliers;this.inventory=inventory;this.finance=finance;this.scheduler=scheduler;this.clock=clock;this.random=random;this.settings=settings;settings.Validate();subscriptions.Subscribe<DeliveryDue>(Deliver);}
        public ProcurementQuote Quote(string supplier,string material,double quantity,ShippingMethod shipping,int transportHours=0,double transportCost=0)
        {
            if(disposed)throw new ObjectDisposedException(nameof(MaterialProcurementSystem));ConstructionValidation.Positive(quantity,"Quantity");if(!Enum.IsDefined(typeof(ShippingMethod),shipping))throw new ArgumentException("Shipping method.");
            if(transportHours<0)throw new ArgumentOutOfRangeException(nameof(transportHours));ConstructionValidation.NonNegative(transportCost,"Transport cost");
            var offer=suppliers.Offer(supplier,material);if(quantity>offer.Capacity-offer.Reserved)throw new InvalidOperationException("Supplier capacity is insufficient.");
            double discount=quantity>=settings.BulkThreshold ? settings.BulkDiscount : 0;
            ConstructionValidation.Unit(ResearchDiscountFraction,"Research discount");if(ResearchDiscountFraction>0)discount=1-(1-discount)*(1-Math.Min(.9,ResearchDiscountFraction));
            double total=Math.Round(quantity*offer.CurrentPrice*(1-discount)*(shipping==ShippingMethod.Expedited ? 1+settings.ExpeditedPremium : 1),2,MidpointRounding.AwayFromZero);
            ConstructionValidation.Positive(total,"Order total");if(total>(double)decimal.MaxValue)throw new ArgumentOutOfRangeException(nameof(quantity));
            int lead=shipping==ShippingMethod.Expedited ? Math.Max(1,Math.Min(offer.LeadHours-1,(int)Math.Ceiling(offer.LeadHours*settings.ExpeditedTimeFactor))) : offer.LeadHours;
            return new ProcurementQuote(offer.CurrentPrice,discount,total+transportCost,checked(lead+transportHours));
        }
        public ProcurementOrder PlaceOrder(string project,string supplier,string material,double quantity,ShippingMethod shipping,int transportHours=0,double transportCost=0)
        {
            ConstructionValidation.Text(project,"Project");var quote=Quote(supplier,material,quantity,shipping,transportHours,transportCost);
            var order=new ProcurementOrder {Id="procurement-"+(orders.Count+1),ProjectId=project,SupplierId=supplier,MaterialId=material,Quantity=quantity,Shipping=shipping,UnitPrice=quote.UnitPrice,Discount=quote.Discount,Cost=quote.Total,DueHour=checked(clock.TotalHours+quote.LeadHours)};
            suppliers.Offer(supplier,material).Reserved+=quantity;orders.Add(order);
            scheduled.Add(scheduler.Schedule(order.DueHour,new DeliveryDue(this,order)));
            finance.AccrueMaterialPurchase(project,(decimal)order.Cost,order.Id,clock.TotalHours);
            GameEvents.Publish(new ProcurementChangedEvent(project));return order;
        }
        private void Deliver(DeliveryDue due)
        {
            if(disposed || !ReferenceEquals(due.Owner,this) || due.Order.Delivered)return;
            var order=due.Order;var supplier=suppliers.Get(order.SupplierId);
            if(!order.ReliabilityChecked)
            {
                order.ReliabilityChecked=true;
                if(random.NextDouble()>=supplier.Reliability)
                {
                    order.Status=ProcurementStatus.Delayed;order.DueHour=checked(clock.TotalHours+settings.DelayHours);
                    scheduled.Add(scheduler.Schedule(order.DueHour,new DeliveryDue(this,order)));
                    GameEvents.Publish(new MaterialDelayedEvent(order.Id,order.ProjectId,settings.DelayHours,"Supplier dispatch delay"));
                    GameEvents.Publish(new ProcurementChangedEvent(order.ProjectId));return;
                }
            }
            inventory.Add(order.MaterialId,order.Quantity);order.Status=ProcurementStatus.Delivered;
            var offer=suppliers.Offer(order.SupplierId,order.MaterialId);offer.Reserved=Math.Max(0,offer.Reserved-order.Quantity);
            supplier.Relationship=Math.Min(1,supplier.Relationship+settings.RelationshipGain);
            GameEvents.Publish(new MaterialDeliveredEvent(order.Id,order.ProjectId,order.MaterialId,(float)order.Quantity));GameEvents.Publish(new ProcurementChangedEvent(order.ProjectId));
        }
        public void Dispose() {if(disposed)return;disposed=true;subscriptions.Dispose();foreach(long id in scheduled)scheduler.Cancel(id);}
    }
}
