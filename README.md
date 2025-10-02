# D²-WORX — Decisive Distributed Application Framework for DCSV WORX
D²-WORX is the distributed evolution of the Decisive Commerce Application Framework (DeCAF). It is an effort to create a scalable foundation for modern SMB-focused SaaS applications, with an emphasis on strong developer experience, and future commercial deployment for WORX or other products.

## Project Status 🚨
This project is in its **earliest stages**. The public repo documents the ongoing evolution of D² as it transitions from a modular monolith (DeCAF) into a distributed framework. Expect frequent changes and incremental progress.

## Quickstart Guide 🚀

### Getting started with local dev environment:
1. Using a command line utility, navigate to the AppHost folder (`/backends/AppHost`) and set the following secrets using the `dotnet user-secrets set <key> <value>` command:

    - `Parameters:mq-username` (RabbitMQ username)
    - `Parameters:mq-password` (RabbitMQ password)
    - `Parameters:db-username` (PostgreSQL username)
    - `Parameters:db-password` (PostgreSQL password)
    - `Parameters:cache-password` (Redis password)
    - `Parameters:kc-username` (Keycloak username)
    - `Parameters:kc-password` (Keycloak password)

    If you want to verify your entries, use the `dotnet user-secrets list` command.

2. Edit any `appsettings.*.json` files as needed.
3. Run the AppHost project.

*To be continued...*

## Philosophy 🤔
**Distributed, Scalable**: built around bounded contexts and event-driven communication to support horizontal scalability.

**Developer-focused**: prioritizes maintainability and DX. Write minimum code with maximum power and intent.

**Pragmatic**: Balances modern patterns with real-world constraints for SMB SaaS.

## Architecture 🏗️
A high-level architecture diagram and documentation will be added here as the project evolves.
For now, see the [Story & Background](#story--background-) section for context on D² and its lineage.

## Story & Background 🌙

### DCSV
DCSV (or "Decisive") is a technology startup founded by [@Tr-st-n](http://github.com/tr-st-n) to create software for SMBs.

### DeCAF
DeCAF (Decisive Commerce Application Framework) is [@Tr-st-n](http://github.com/tr-st-n)'s Nth attempt at building a modular monolithic web application that can serve as a base for various products. Its third iteration features a .NET 9 back end and a SvelteKit front end, backed by PostgreSQL, Redis, and other dependencies.

DeCAF uses interfaces and settings to decouple "features" (modules) and "providers", allowing cross-communication without a fully distributed architecture. While still deployed with CI/CD and Docker, this simplified design is ideal for small-to-medium traffic apps, saving significant dev time and improving DX compared to traditional N-tier and distributed approaches.

DeCAF v1 and v2 are in production use by thousands of users (closed source). Out of the box, DeCAF provides authentication, authorization, multi-tenant organization management, invoicing, billing, payments, payouts, products, categories, tagging, checkout, payment methods, account credits, administration, and affiliate dashboards, among other features.

### D²
D² (Decisive Distributed Application Framework) is the distributed evolution of DeCAF v3. It is built with .NET Aspire (.NET 10 / C# 14), retains a SvelteKit front end, and uses PostgreSQL as its core relational database. The goal of D² is to provide a **horizontally scalable successor** to DeCAF while keeping the strong developer experience.

### WORX
WORX (pronounced "works") is a SaaS product [@Tr-st-n](http://github.com/tr-st-n) is developing for SMBs, including sole proprietors running time-and-materials businesses. Its focus is **workflow automation, client management, invoicing, and communication** — all powered by the evolving D² framework.

While WORX itself will be a commercial product, this repository exists (for now, publicly) as a **reference implementation of D²**, showing how the framework builds on DeCAF and adapts it into a distributed system while maintaining the same empowering DX.

## License 📜
This project is protected by the [PolyForm Strict License 1.0.0](https://polyformproject.org/licenses/strict/1.0.0). See [LICENSE.md](/LICENSE.md) for more information.

Summary:

✅ Free to view, fork, and run locally for learning and evaluation.

❌ Not permitted for production or commercial use without explicit permission.