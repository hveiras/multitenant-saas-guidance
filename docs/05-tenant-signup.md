# Sign-up and tenant onboarding

This chapter describes how to implement a _sign-up_ process in a multi-tenant application, which allows a customer to sign up their organization for your application.
There are several reasons to implement a sign-up process:

-	Allow an AD admin to consent for the customer's entire organization to use the application.
-	Collect credit card payment or other customer information.
-	Perform any one-time per-tenant setup needed by your application.

## Admin consent and Azure AD permissions

In order to authenticate with Azure AD, an application needs access to the user's directory. At a minimum, the application needs permission to read the user's profile. The first time that a user signs in, Azure AD shows a consent page that lists the permissions being requested. By clicking **Accept**, the user grants permission to the application.

By default, consent is granted on a per-user basis. Every user who signs in sees the consent page. However, Azure AD also supports  _admin consent_, which allows an AD administrator to consent for an entire organization.

When the admin consent flow is used, the consent page states that the AD admin is granting permission on behalf of the entire tenant:

![Admin consent prompt](media/sign-up/admin-consent.png)

After the admin clicks **Accept**, other users within the same tenant can sign in, and Azure AD will skip the consent screen.

Only an AD administrator can give admin consent, because it grants permission on behalf of the entire organization. If a non-administrator tries to authenticate with the admin consent flow, Azure AD displays an error:

If at a later point the application requires additional permissions, the customer will need to remove the application from the tenant and sign up again, in order to consent to the updated permissions.  

![Consent error](media/sign-up/consent-error.png)


# Implementing tenant sign-up

For the Tailspin Surveys application,  we defined several requirements for the sign-up process:

-	A tenent must sign up before users can sign in.
-	Sign-up uses the admin consent flow.
-	Sign-up adds the user's tenant to the application database.
-	After a tenant signs up, the application shows an onboarding page.

In this section, we'll walk through our implementation of the sign-up process.
It's important to understand that "sign up" versus "sign in" is an application concept. During the authentication flow, Azure AD does not inherently know whether the user is in process of signing up. It's up to the application to keep track of the context.

When an anonymous user visits the Surveys application, the user is shown two buttons, one to sign in, and one to "enroll your company" (sign up).

![Application sign-up page](media/sign-up/sign-up-page.png)

These buttons invoke actions in the [AccountController](https://github.com/mspnp/multitenant-saas-guidance/blob/master/src/Tailspin.Surveys.Web/Controllers/AccountController.cs) class.

The `SignIn` action returns a **ChallegeResult**, which causes the OpenID Connect middleware to redirect to the authentication endpoint. This is the default way to trigger authentication in ASP.NET 5.  

```
[AllowAnonymous]
public IActionResult SignIn()
{
    return new ChallengeResult(
        OpenIdConnectDefaults.AuthenticationScheme,
        new AuthenticationProperties
        {
            RedirectUri = Url.Action("SignInCallback", "Account")
        });
}
```

Now compare the `SignUp` action:

    [AllowAnonymous]
    public IActionResult SignUp()
    {
        // Workaround for https://github.com/aspnet/Security/issues/546
        HttpContext.Items.Add("signup", "true");

        var state = new Dictionary<string, string> { { "signup", "true" }};
        return new ChallengeResult(
            OpenIdConnectDefaults.AuthenticationScheme,
            new AuthenticationProperties(state)
            {
                RedirectUri = Url.Action(nameof(SignUpCallback), "Account")
            });
    }

> This code includes a workaround for a known bug in ASP.NET 5 RC1. See the [Admin Consent](#adding-the-admin-consent-prompt) section for more information.

Like `SignIn`, the `SignUp` action also returns a `ChallengeResult`. But this time, we add a piece of state information to the `AuthenticationProperties` in the `ChallengeResult`:

-	signup: A Boolean flag, indicating that the user has started the sign-up process.

The state information in `AuthenticationProperties` gets added to the OpenID Connect [state](http://openid.net/specs/openid-connect-core-1_0.html#AuthRequest) parameter, which round trips during the authentication flow.

![State parameter](media/sign-up/state-parameter.png)

After the user authenticates in Azure AD and gets redirected back to the application, the authentication ticket contains the state. We are using this fact to make sure the "signup" value persists acrross the entire authentication flow.

## Adding the admin consent prompt

In Azure AD, the admin consent flow is triggered by adding a "prompt" parameter to the query string in the authentication request:

    /authorize?prompt=admin_consent&...

The Surveys application adds the prompt during the `RedirectToAuthenticationEndpoint` event. This event is called right before the middleware redirects to the authentication endpoint.

```
public override Task RedirectToAuthenticationEndpoint(RedirectContext context)
{
    if (context.IsSigningUp())
    {
        context.ProtocolMessage.Prompt = "admin_consent";
    }

    _logger.RedirectToIdentityProvider();
    return Task.FromResult(0);
}
```

> See [SurveyAuthenticationEvents.cs](https://github.com/mspnp/multitenant-saas-guidance/blob/master/src/Tailspin.Surveys.Web/Security/SurveyAuthenticationEvents.cs).

Setting` ProtocolMessage.Prompt` tells the middleware to add the "prompt" parameter to the authentication request.

Note that the prompt is only needed during sign-up. Regular sign-in should not include it. To distinguish between them, we check for the `signup` value in the authentication state. The following extension method checks for this condition:

```
internal static bool IsSigningUp(this BaseControlContext context)
{
    if (context == null)
    {
        throw new ArgumentNullException(nameof(context));
    }

    string signupValue;
    object obj;
    // Check the HTTP context and convert to string
    if (context.HttpContext.Items.TryGetValue("signup", out obj))
    {
        signupValue = (string)obj;
    }
    else
    {
        // It's not in the HTTP context, so check the authentication ticket.  If it's not there, we aren't signing up.
        if ((context.AuthenticationTicket == null) ||
            (!context.AuthenticationTicket.Properties.Items.TryGetValue("signup", out signupValue)))
        {
            return false;
        }
    }

    // We have found the value, so see if it's valid
    bool isSigningUp;
    if (!bool.TryParse(signupValue, out isSigningUp))
    {
        // The value for signup is not a valid boolean, throw                
        throw new InvalidOperationException($"'{signupValue}' is an invalid boolean value");
    }

    return isSigningUp;
}
```

> See [BaseControlContextExtensions.cs](https://github.com/mspnp/multitenant-saas-guidance/blob/master/src/Tailspin.Surveys.Web/Security/BaseControlContextExtensions.cs).

> Note: This code includes a workaround for a known bug in ASP.NET 5 RC1. In the `RedirectToAuthenticationEndpoint` event, there is no way to get the authentication properties that contains the "signup" state. As a workaround, the `AccountController.SignUp` method also puts the "signup" state into the `HttpContext`. This works because `RedirectToAuthenticationEndpoint` happens before the redirect, so we still have the same `HttpContext`.

## Registering a Tenant

The Surveys application stores some information about each tenant and user in the application database.

![Tenant table](media/sign-up/tenant-table.png)

In the Tenant table, IssuerValue is the value of the issuer claim for the tenant. For Azure AD, this is `https://sts.windows.net/<tentantID>` and gives a unique value per tenant.

When a new tenant signs up, the Surveys application writes a tenant record to the database. This happens inside the `AuthenticationValidated` event. (Don't do it before this event, because the ID token won't be validated yet, so you can't trust the claim values. See [Authentication](03-authentication.md)).

Here is the relevant code from the Surveys application:

    public override async Task AuthenticationValidated(AuthenticationValidatedContext context)
    {
        var principal = context.AuthenticationTicket.Principal;
        try
        {
            var userId = principal.GetObjectIdentifierValue();
            var tenantManager = context.HttpContext.RequestServices.GetService<TenantManager>();
            var userManager = context.HttpContext.RequestServices.GetService<UserManager>();
            var issuerValue = principal.GetIssuerValue();

            // Normalize the claims first.
            NormalizeClaims(principal);
            var tenant = await tenantManager.FindByIssuerValueAsync(issuerValue);

            if (context.IsSigningUp())
            {
                if (tenant == null)
                {
                    tenant = await SignUpTenantAsync(context, tenantManager);
                }

                // In this case, we need to go ahead and set up the user signing us up.
                await CreateOrUpdateUserAsync(context.AuthenticationTicket, userManager, tenant);
            }
            else
            {
                if (tenant == null)
                {
                    throw new SecurityTokenValidationException($"Tenant {issuerValue} is not registered");
                }

                await CreateOrUpdateUserAsync(context.AuthenticationTicket, userManager, tenant);
            }

        }
        catch
        {
            // Handle error (not shown)
        }
    }

> See [SurveyAuthenticationEvents.cs](https://github.com/mspnp/multitenant-saas-guidance/blob/master/src/Tailspin.Surveys.Web/Security/SurveyAuthenticationEvents.cs). This code snippet omits some logging and other details that aren't relevant to this discussion.

This code does the following:

1.	Check if the tenant's issuer value is already in the database. If the tenant has not signed up, `FindByIssuerValueAsync` returns null.
2.	If the user is signing up:
  1.	Add the tenant to the database (`SignUpTenantAsync`).
  2.	Add the authenticated user to the database (`CreateOrUpdateUserAsync`).
3.	Otherwise complete the normal sign-in flow:
  1.	If the tenant's issuer was not found in the database, it means the tenant is not registered, and the customer needs to sign up. In that case, throw an exception to cause the authentication to fail.
  2.	Otherwise, create a database record for this user, if there isn't one already (`CreateOrUpdateUserAsync`).

Here is the [SignUpTenantAsync](https://github.com/mspnp/multitenant-saas-guidance/blob/master/src/Tailspin.Surveys.Web/Security/SurveyAuthenticationEvents.cs) method that adds the tenant to the database.

    private async Task<Tenant> SignUpTenantAsync(BaseControlContext context, TenantManager tenantManager)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (tenantManager == null)
        {
            throw new ArgumentNullException(nameof(tenantManager));
        }

        var principal = context.AuthenticationTicket.Principal;
        var issuerValue = principal.GetIssuerValue();
        var tenant = new Tenant
        {
            IssuerValue = issuerValue,
            Created = DateTimeOffset.UtcNow
        };

        try
        {
            await tenantManager.CreateAsync(tenant);
        }
        catch(Exception ex)
        {
            _logger.SignUpTenantFailed(principal.GetObjectIdentifierValue(), issuerValue, ex);
            throw;
        }

        return tenant;
    }

> Note: If you try to sign up the tenant that is hosting the app, Azure AD returns a generic error. To avoid this, you can seed the database with the SaaS provider's tenant.

Here is a summary of the entire sign-up flow in the Surveys application:

1.	The user clicks the **Sign Up** button.
2.	The `AccountController.SignUp` action returns a challege result.  The authentication state includes "signup" value.
3.	In the `RedirectToAuthenticationEndpoint` event, add the `admin_consent` prompt.
4.	The OpenID Connect middleware redirects to Azure AD and the user authenticates.
5.	In the `AuthenticationValidated` event, look for the "signup" state.
6.	Add the tenant to the database.
