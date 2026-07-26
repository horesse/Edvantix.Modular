using EDV.Modules.Billing.Contracts.Dtos;
using Mediator;

namespace EDV.Modules.Billing.Contracts.v1.Wallets;

public sealed record GetMyWalletQuery : IQuery<WalletDto>;
