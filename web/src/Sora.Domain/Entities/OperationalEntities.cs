namespace Sora.Domain.Entities;

public sealed class StaffRole
{
    private StaffRole() { }

    public StaffRole(string code, string displayName)
    {
        Code = code;
        DisplayName = displayName;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public ICollection<StaffAccountRole> StaffAssignments { get; } = new List<StaffAccountRole>();
    public ICollection<StaffRolePermission> Permissions { get; } = new List<StaffRolePermission>();
}

public sealed class StaffPermission
{
    private StaffPermission() { }
    public StaffPermission(string code, string displayName)
    {
        Code = code;
        DisplayName = displayName;
    }
    public string Code { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public ICollection<StaffRolePermission> Roles { get; } = new List<StaffRolePermission>();
}

public sealed class StaffRolePermission
{
    private StaffRolePermission() { }
    public StaffRolePermission(string roleCode, string permissionCode)
    {
        RoleCode = roleCode;
        PermissionCode = permissionCode;
    }
    public string RoleCode { get; private set; } = string.Empty;
    public string PermissionCode { get; private set; } = string.Empty;
    public StaffRole Role { get; private set; } = null!;
    public StaffPermission Permission { get; private set; } = null!;
}

public sealed class StaffAuditEntry
{
    private StaffAuditEntry() { }
    public StaffAuditEntry(Guid actorId, string action, string targetType, string targetId,
        string details, DateTimeOffset occurredAtUtc)
    {
        Id = Guid.NewGuid();
        ActorId = actorId;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        Details = details;
        OccurredAtUtc = occurredAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid ActorId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public string TargetId { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public StaffAccount Actor { get; private set; } = null!;
}

public sealed class StaffAccountRole
{
    private StaffAccountRole() { }

    public StaffAccountRole(Guid staffAccountId, string roleCode, DateTimeOffset assignedAtUtc, Guid? assignedById = null)
    {
        StaffAccountId = staffAccountId;
        RoleCode = roleCode;
        AssignedAtUtc = assignedAtUtc;
        AssignedById = assignedById;
    }

    public Guid StaffAccountId { get; private set; }
    public string RoleCode { get; private set; } = string.Empty;
    public Guid? AssignedById { get; private set; }
    public DateTimeOffset AssignedAtUtc { get; private set; }
    public StaffAccount StaffAccount { get; private set; } = null!;
    public StaffRole Role { get; private set; } = null!;
    public StaffAccount? AssignedBy { get; private set; }
}

public sealed class Customer
{
    private Customer() { }

    public Customer(string customerType, string fullName, string phone, string primaryAddress,
        decimal? latitude, decimal? longitude, DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        CustomerType = customerType;
        FullName = fullName;
        Phone = phone;
        PrimaryAddress = primaryAddress;
        Latitude = latitude;
        Longitude = longitude;
        CreditBalance = 0m;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string CustomerType { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string PrimaryAddress { get; private set; } = string.Empty;
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public decimal CreditBalance { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void Update(string customerType, string fullName, string phone, string primaryAddress,
        decimal? latitude, decimal? longitude)
    {
        CustomerType = customerType;
        FullName = fullName;
        Phone = phone;
        PrimaryAddress = primaryAddress;
        Latitude = latitude;
        Longitude = longitude;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
    public bool TryConsumeCredit(decimal amount)
    {
        if (amount <= 0 || amount > CreditBalance) return false;
        CreditBalance = decimal.Round(CreditBalance - amount, 2, MidpointRounding.AwayFromZero);
        return true;
    }
    public void GrantCredit(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        CreditBalance = decimal.Round(CreditBalance + amount, 2, MidpointRounding.AwayFromZero);
    }
}

public sealed class Product
{
    private Product() { }

    public Product(string internalCode, string? barcode, string name, string? brand, string? category,
        decimal? netContent, string? contentUnit, string baseUnit, decimal unitSalePrice, DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        InternalCode = internalCode;
        Barcode = barcode;
        Name = name;
        Brand = brand;
        Category = category;
        NetContent = netContent;
        ContentUnit = contentUnit;
        BaseUnit = baseUnit;
        UnitSalePrice = unitSalePrice;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string InternalCode { get; private set; } = string.Empty;
    public string? Barcode { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Brand { get; private set; }
    public string? Category { get; private set; }
    public decimal? NetContent { get; private set; }
    public string? ContentUnit { get; private set; }
    public string BaseUnit { get; private set; } = "unidad";
    public decimal UnitSalePrice { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public ICollection<ProductPresentation> Presentations { get; } = new List<ProductPresentation>();
    public ICollection<ProductDiscountRule> DiscountRules { get; } = new List<ProductDiscountRule>();

    public void Update(string internalCode, string? barcode, string name, string? brand, string? category,
        decimal? netContent, string? contentUnit, string baseUnit, decimal unitSalePrice)
    {
        InternalCode = internalCode;
        Barcode = barcode;
        Name = name;
        Brand = brand;
        Category = category;
        NetContent = netContent;
        ContentUnit = contentUnit;
        BaseUnit = baseUnit;
        UnitSalePrice = unitSalePrice;
    }

    public ProductPresentation AddPresentation(string name, int unitsPerPresentation)
    {
        var presentation = new ProductPresentation(Id, name, unitsPerPresentation);
        Presentations.Add(presentation);
        return presentation;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}

public sealed class ProductPresentation
{
    private ProductPresentation() { }

    public ProductPresentation(Guid productId, string name, int unitsPerPresentation)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        Name = name;
        UnitsPerPresentation = unitsPerPresentation;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int UnitsPerPresentation { get; private set; }
    public bool IsActive { get; private set; }
    public Product Product { get; private set; } = null!;

    public void Update(string name, int unitsPerPresentation)
    {
        Name = name;
        UnitsPerPresentation = unitsPerPresentation;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}

public sealed class ProductDiscountRule
{
    private ProductDiscountRule() { }
    public ProductDiscountRule(Guid productId, int minimumQuantityUnits, decimal discountPercent)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        MinimumQuantityUnits = minimumQuantityUnits;
        DiscountPercent = discountPercent;
    }
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public int MinimumQuantityUnits { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public Product Product { get; private set; } = null!;
}

public sealed class Supplier
{
    private Supplier() { }

    public Supplier(string name, string phone)
    {
        Id = Guid.NewGuid();
        Name = name;
        Phone = phone;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public ICollection<SupplierProduct> ProductLinks { get; } = new List<SupplierProduct>();

    public void Update(string name, string phone)
    {
        Name = name;
        Phone = phone;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}

public sealed class SupplierProduct
{
    private SupplierProduct() { }
    public SupplierProduct(Guid supplierId, Guid productId)
    {
        SupplierId = supplierId;
        ProductId = productId;
    }
    public Guid SupplierId { get; private set; }
    public Guid ProductId { get; private set; }
    public Supplier Supplier { get; private set; } = null!;
    public Product Product { get; private set; } = null!;
}

public sealed class PurchaseReceipt
{
    private PurchaseReceipt() { }

    public PurchaseReceipt(Guid supplierId, string invoiceNumber, DateTimeOffset receivedAtUtc, Guid receivedById)
    {
        Id = Guid.NewGuid();
        SupplierId = supplierId;
        InvoiceNumber = invoiceNumber;
        ReceivedAtUtc = receivedAtUtc;
        ReceivedById = receivedById;
    }

    public Guid Id { get; private set; }
    public Guid SupplierId { get; private set; }
    public string InvoiceNumber { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public Guid ReceivedById { get; private set; }
    public Supplier Supplier { get; private set; } = null!;
    public StaffAccount ReceivedBy { get; private set; } = null!;
    public ICollection<InventoryLot> Lots { get; } = new List<InventoryLot>();
}

public sealed class InventoryLot
{
    private InventoryLot() { }

    public InventoryLot(Guid receiptId, Guid productId, string lotCode, DateOnly? expiryDate,
        int quantityReceived, decimal unitPurchaseCost)
    {
        Id = Guid.NewGuid();
        ReceiptId = receiptId;
        ProductId = productId;
        LotCode = lotCode;
        ExpiryDate = expiryDate;
        QuantityReceived = quantityReceived;
        UnitPurchaseCost = unitPurchaseCost;
    }

    public Guid Id { get; private set; }
    public Guid ReceiptId { get; private set; }
    public Guid ProductId { get; private set; }
    public string LotCode { get; private set; } = string.Empty;
    public DateOnly? ExpiryDate { get; private set; }
    public int QuantityReceived { get; private set; }
    public decimal UnitPurchaseCost { get; private set; }
    public PurchaseReceipt Receipt { get; private set; } = null!;
    public Product Product { get; private set; } = null!;
    public InventoryLotBalance Balance { get; private set; } = null!;
    public ICollection<InventoryMovement> Movements { get; } = new List<InventoryMovement>();
}

public sealed class InventoryLotBalance
{
    private InventoryLotBalance() { }

    public InventoryLotBalance(Guid lotId, int onHandUnits, int reservedUnits = 0)
    {
        LotId = lotId;
        OnHandUnits = onHandUnits;
        ReservedUnits = reservedUnits;
    }

    public Guid LotId { get; private set; }
    public int OnHandUnits { get; private set; }
    public int ReservedUnits { get; private set; }
    public int AvailableUnits => OnHandUnits - ReservedUnits;
    public InventoryLot Lot { get; private set; } = null!;

    public bool TryAdjustOnHand(int delta)
    {
        var nextOnHand = OnHandUnits + delta;
        if (nextOnHand < ReservedUnits || nextOnHand < 0)
        {
            return false;
        }
        OnHandUnits = nextOnHand;
        return true;
    }

    public bool TryChangeReserved(int delta)
    {
        var nextReserved = ReservedUnits + delta;
        if (nextReserved < 0 || nextReserved > OnHandUnits)
        {
            return false;
        }
        ReservedUnits = nextReserved;
        return true;
    }
}

public sealed class InventoryMovement
{
    private InventoryMovement() { }

    public InventoryMovement(Guid lotId, string movementType, int onHandDelta, int reservedDelta,
        Guid actorId, DateTimeOffset occurredAtUtc, string? reason = null, Guid? orderId = null,
        Guid? dispatchOrderId = null, Guid? correctsMovementId = null)
    {
        Id = Guid.NewGuid();
        LotId = lotId;
        MovementType = movementType;
        OnHandDelta = onHandDelta;
        ReservedDelta = reservedDelta;
        ActorId = actorId;
        OccurredAtUtc = occurredAtUtc;
        Reason = reason;
        OrderId = orderId;
        DispatchOrderId = dispatchOrderId;
        CorrectsMovementId = correctsMovementId;
    }

    public Guid Id { get; private set; }
    public Guid LotId { get; private set; }
    public string MovementType { get; private set; } = string.Empty;
    public int OnHandDelta { get; private set; }
    public int ReservedDelta { get; private set; }
    public Guid? OrderId { get; private set; }
    public Guid? DispatchOrderId { get; private set; }
    public Guid ActorId { get; private set; }
    public string? Reason { get; private set; }
    public Guid? CorrectsMovementId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public InventoryLot Lot { get; private set; } = null!;
    public StaffAccount Actor { get; private set; } = null!;
    public SalesOrder? Order { get; private set; }
    public DispatchOrder? DispatchOrder { get; private set; }
    public InventoryMovement? CorrectsMovement { get; private set; }
}

public sealed class SalesOrder
{
    private SalesOrder() { }

    public SalesOrder(string orderNumber, Guid customerId, Guid createdById, string status,
        string fulfillment, string? deliveryAddressSnapshot, decimal? deliveryLatitude,
        decimal? deliveryLongitude, decimal totalAmount, DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        OrderNumber = orderNumber;
        CustomerId = customerId;
        CreatedById = createdById;
        Status = status;
        Fulfillment = fulfillment;
        DeliveryAddressSnapshot = deliveryAddressSnapshot;
        DeliveryLatitude = deliveryLatitude;
        DeliveryLongitude = deliveryLongitude;
        TotalAmount = totalAmount;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Guid CreatedById { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string Fulfillment { get; private set; } = string.Empty;
    public bool IsNightOrder { get; private set; }
    public DateOnly? ScheduledDeliveryDate { get; private set; }
    public string? DeliveryAddressSnapshot { get; private set; }
    public decimal? DeliveryLatitude { get; private set; }
    public decimal? DeliveryLongitude { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Customer Customer { get; private set; } = null!;
    public StaffAccount CreatedBy { get; private set; } = null!;
    public ICollection<OrderLine> Lines { get; } = new List<OrderLine>();
    public ICollection<InventoryMovement> Movements { get; } = new List<InventoryMovement>();
    public ICollection<DispatchOrder> DispatchAssignments { get; } = new List<DispatchOrder>();
    public ICollection<OrderHistoryEntry> History { get; } = new List<OrderHistoryEntry>();
    public ICollection<OrderPayment> Payments { get; } = new List<OrderPayment>();

    public void SetStatus(string status) => Status = status;
    public void SetTotalAmount(decimal totalAmount) => TotalAmount = totalAmount;
    public void ScheduleNightDelivery(DateOnly deliveryDate)
    {
        IsNightOrder = true;
        ScheduledDeliveryDate = deliveryDate;
    }
    public void UpdateFulfillment(string fulfillment, string? address, decimal? latitude, decimal? longitude)
    {
        Fulfillment = fulfillment;
        DeliveryAddressSnapshot = address;
        DeliveryLatitude = latitude;
        DeliveryLongitude = longitude;
    }
}

public sealed class OrderPayment
{
    private OrderPayment() { }
    public OrderPayment(Guid orderId, decimal amount, string reference, Guid recordedById, DateTimeOffset recordedAtUtc, string kind = "CASH")
    {
        Id = Guid.NewGuid(); OrderId = orderId; Amount = amount; Reference = reference;
        RecordedById = recordedById; RecordedAtUtc = recordedAtUtc; Kind = kind;
    }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public string Reference { get; private set; } = string.Empty;
    public string Kind { get; private set; } = "CASH";
    public Guid RecordedById { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public SalesOrder Order { get; private set; } = null!;
    public StaffAccount RecordedBy { get; private set; } = null!;
}

public sealed class OrderCancellationResolution
{
    private OrderCancellationResolution() { }
    public OrderCancellationResolution(Guid orderId, string resolution, decimal resolvedAmount,
        decimal cancellationFee, string? reference, Guid actorId, DateTimeOffset resolvedAtUtc)
    {
        Id = Guid.NewGuid(); OrderId = orderId; Resolution = resolution;
        ResolvedAmount = resolvedAmount; CancellationFee = cancellationFee;
        Reference = reference; ActorId = actorId; ResolvedAtUtc = resolvedAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Resolution { get; private set; } = "NONE";
    public decimal ResolvedAmount { get; private set; }
    public decimal CancellationFee { get; private set; }
    public string? Reference { get; private set; }
    public Guid ActorId { get; private set; }
    public DateTimeOffset ResolvedAtUtc { get; private set; }
    public SalesOrder Order { get; private set; } = null!;
    public StaffAccount Actor { get; private set; } = null!;
}

public sealed class OrderLine
{
    private OrderLine() { }

    public OrderLine(Guid orderId, Guid productId, Guid? presentationId, decimal enteredQuantity,
        int quantityUnits, decimal unitSalePriceSnapshot, decimal lineTotal,
        int unitsAvailableAtCreation = 0, int shortageUnits = 0)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        ProductId = productId;
        PresentationId = presentationId;
        EnteredQuantity = enteredQuantity;
        QuantityUnits = quantityUnits;
        UnitSalePriceSnapshot = unitSalePriceSnapshot;
        SubtotalAmount = decimal.Round(quantityUnits * unitSalePriceSnapshot, 2, MidpointRounding.AwayFromZero);
        LineTotal = lineTotal;
        DiscountAmount = SubtotalAmount - lineTotal;
        UnitsAvailableAtCreation = unitsAvailableAtCreation;
        ShortageUnits = shortageUnits;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? PresentationId { get; private set; }
    public decimal EnteredQuantity { get; private set; }
    public int QuantityUnits { get; private set; }
    public decimal UnitSalePriceSnapshot { get; private set; }
    public decimal SubtotalAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal LineTotal { get; private set; }
    public int UnitsAvailableAtCreation { get; private set; }
    public int ShortageUnits { get; private set; }
    public SalesOrder Order { get; private set; } = null!;
    public Product Product { get; private set; } = null!;
    public ProductPresentation? Presentation { get; private set; }
    public ICollection<OrderLotAllocation> LotAllocations { get; } = new List<OrderLotAllocation>();

    public void ApplyDiscount(decimal discountAmount)
    {
        DiscountAmount = Math.Clamp(decimal.Round(discountAmount, 2, MidpointRounding.AwayFromZero),
            0, SubtotalAmount);
        LineTotal = SubtotalAmount - DiscountAmount;
    }
}

public sealed class OrderHistoryEntry
{
    private OrderHistoryEntry() { }
    public OrderHistoryEntry(Guid orderId, Guid actorId, string eventType, string details,
        DateTimeOffset occurredAtUtc)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        ActorId = actorId;
        EventType = eventType;
        Details = details;
        OccurredAtUtc = occurredAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ActorId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public SalesOrder Order { get; private set; } = null!;
    public StaffAccount Actor { get; private set; } = null!;
}

public sealed class OrderLotAllocation
{
    private OrderLotAllocation() { }

    public OrderLotAllocation(Guid orderLineId, Guid lotId, int quantityReserved,
        decimal unitCostSnapshot)
    {
        Id = Guid.NewGuid();
        OrderLineId = orderLineId;
        LotId = lotId;
        QuantityReserved = quantityReserved;
        UnitCostSnapshot = unitCostSnapshot;
    }

    public Guid Id { get; private set; }
    public Guid OrderLineId { get; private set; }
    public Guid LotId { get; private set; }
    public int QuantityReserved { get; private set; }
    public int QuantityPicked { get; private set; }
    public decimal UnitCostSnapshot { get; private set; }
    public OrderLine OrderLine { get; private set; } = null!;
    public InventoryLot Lot { get; private set; } = null!;

    public void SetPicked(int quantity) => QuantityPicked = quantity;
}

public sealed class Dispatch
{
    private Dispatch() { }

    public Dispatch(string dispatchNumber, string vehicleIdentifier, Guid dispatchLeadId,
        Guid driverStaffId, DateTimeOffset departedAtUtc, DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        DispatchNumber = dispatchNumber;
        VehicleIdentifier = vehicleIdentifier;
        DispatchLeadId = dispatchLeadId;
        DriverStaffId = driverStaffId;
        DepartedAtUtc = departedAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string DispatchNumber { get; private set; } = string.Empty;
    public string VehicleIdentifier { get; private set; } = string.Empty;
    public Guid DispatchLeadId { get; private set; }
    public Guid DriverStaffId { get; private set; }
    public DateTimeOffset DepartedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public StaffAccount DispatchLead { get; private set; } = null!;
    public StaffAccount Driver { get; private set; } = null!;
    public ICollection<DispatchOrder> Orders { get; } = new List<DispatchOrder>();
}

public sealed class DispatchOrder
{
    private DispatchOrder() { }

    public DispatchOrder(Guid dispatchId, Guid orderId, short attemptNumber, int sequenceNumber)
    {
        Id = Guid.NewGuid();
        DispatchId = dispatchId;
        OrderId = orderId;
        AttemptNumber = attemptNumber;
        SequenceNumber = sequenceNumber;
    }

    public Guid Id { get; private set; }
    public Guid DispatchId { get; private set; }
    public Guid OrderId { get; private set; }
    public short AttemptNumber { get; private set; }
    public int SequenceNumber { get; private set; }
    public string? Result { get; private set; }
    public string? RecipientName { get; private set; }
    public string? RecipientRelationship { get; private set; }
    public string? Reason { get; private set; }
    public byte[]? SignatureBytes { get; private set; }
    public string? SignatureMimeType { get; private set; }
    public DateTimeOffset? AttemptedAtUtc { get; private set; }
    public DateTimeOffset? InspectedAtUtc { get; private set; }
    public Guid? InspectorId { get; private set; }
    public Guid? ResultRecordedById { get; private set; }
    public string? InspectionNotes { get; private set; }
    public decimal CancellationFeeAmount { get; private set; }
    public Dispatch Dispatch { get; private set; } = null!;
    public SalesOrder Order { get; private set; } = null!;
    public ICollection<InventoryMovement> Movements { get; } = new List<InventoryMovement>();

    public void RecordResult(string result, string? recipientName, string? recipientRelationship,
        string? reason, byte[]? signatureBytes, string? signatureMimeType, DateTimeOffset attemptedAtUtc,
        Guid actorId, decimal cancellationFeeAmount = 0m)
    {
        Result = result;
        RecipientName = recipientName;
        RecipientRelationship = recipientRelationship;
        Reason = reason;
        SignatureBytes = signatureBytes;
        SignatureMimeType = signatureMimeType;
        AttemptedAtUtc = attemptedAtUtc;
        ResultRecordedById = actorId;
        CancellationFeeAmount = cancellationFeeAmount;
    }

    public void CompleteInspection(Guid inspectorId, DateTimeOffset inspectedAtUtc, string notes)
    {
        InspectorId = inspectorId;
        InspectedAtUtc = inspectedAtUtc;
        InspectionNotes = notes;
    }
}
