![Microsoft patterns & practices](http://pnp.azurewebsites.net/images/pnp-logo.png)
# Building Multi-Tenant SaaS Applications on Windows Azure

## Phase 1: Identity Management

This project consists of:

- The Tailspin Surveys application, a reference implementation of a multi-tenant SaaS application.
- Written guidance on best practices for multitenant applications in Microsoft Azure.

The written guidance reflects what we learned in the process of building the application. To get started with the application, see [Running the Surveys application](docs/running-the-app.md).

### Table of Contents

- [Introduction](docs/01-intro.md)
- [About the Tailspin Surveys application](docs/02-tailspin-scenario.md)
- [Authentication with Azure AD](docs/03-authentication.md)
    - How to authenticate users from Azure Active Directory (Azure AD), using OpenID Connect (OIDC) to authenticate
- [Working with claims](docs/04-working-with-claims.md)
- [Sign-up and tenant onboarding](docs/05-tenant-signup.md)
    - How to implement a sign-up process that allows a customer to sign up their organization for your application
- [Application roles](docs/06-application-roles.md)
    - How to define and manage application roles.
- [Authorization](docs/07-authorization.md)
    - Role-based authorization
    - Resource-based authorization
- [Federating with a customer's AD FS](docs/08-adfs.md)
    - How to authenticate via Active Directory Federation Services (AD FS), in order to federate with a customer's on-premise Active Directory.
- Appendixes
    - [Authorization APIs in ASP.NET 5](docs/appendixes/aspnet5-authorization.md)
    - [Overview of OAuth 2 and OpenID Connect](docs/appendixes/about-oauth2-oidc.md)
    - [Using client assertion to get access tokens from Azure AD](docs/appendixes/client-assertion.md)
