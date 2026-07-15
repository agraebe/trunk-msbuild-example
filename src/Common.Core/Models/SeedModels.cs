namespace Contoso.Common.Core.Models;

/// <summary>Plain records mirroring the JSON shape in data/seed/*.json.</summary>
public sealed record Customer(string Id, string Name, string Tier, int Seats);

public sealed record Device(string Id, string CustomerId, string Platform, bool Compliant);

public sealed record Region(string Code, string DisplayName, bool DefaultForNewCustomers);
