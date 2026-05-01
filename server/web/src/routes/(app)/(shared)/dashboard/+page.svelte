<script lang="ts">
  import * as Card from "$lib/client/components/ui/card/index.js";
  import * as m from "$lib/paraglide/messages.js";
</script>

<svelte:head>
  <title>{m.common_ui_dashboard()} — {m.webclient_nav_brand()}</title>
  <meta
    name="description"
    content={m.webclient_app_dashboard_description()}
  />
  <meta
    property="og:title"
    content="{m.common_ui_dashboard()} — {m.webclient_nav_brand()}"
  />
  <meta
    property="og:description"
    content={m.webclient_app_dashboard_description()}
  />
  <meta
    property="og:type"
    content="website"
  />
</svelte:head>

<div class="space-y-6">
  <h1 class="text-3xl font-bold tracking-tight">{m.common_ui_dashboard()}</h1>

  <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
    <Card.Card>
      <Card.CardHeader>
        <Card.CardTitle>{m.webclient_app_dashboard_overview()}</Card.CardTitle>
        <Card.CardDescription
          >{m.webclient_app_dashboard_overview_description()}</Card.CardDescription
        >
      </Card.CardHeader>
      <Card.CardContent>
        <p class="text-muted-foreground text-sm">
          {m.webclient_app_dashboard_overview_placeholder()}
        </p>
      </Card.CardContent>
    </Card.Card>

    <Card.Card>
      <Card.CardHeader>
        <Card.CardTitle>{m.webclient_app_dashboard_recent_activity()}</Card.CardTitle>
        <Card.CardDescription
          >{m.webclient_app_dashboard_recent_activity_description()}</Card.CardDescription
        >
      </Card.CardHeader>
      <Card.CardContent>
        <p class="text-muted-foreground text-sm">
          {m.webclient_app_dashboard_recent_activity_placeholder()}
        </p>
      </Card.CardContent>
    </Card.Card>

    <Card.Card>
      <Card.CardHeader>
        <Card.CardTitle>{m.webclient_app_dashboard_quick_actions()}</Card.CardTitle>
        <Card.CardDescription
          >{m.webclient_app_dashboard_quick_actions_description()}</Card.CardDescription
        >
      </Card.CardHeader>
      <Card.CardContent>
        <p class="text-muted-foreground text-sm">
          {m.webclient_app_dashboard_quick_actions_placeholder()}
        </p>
      </Card.CardContent>
    </Card.Card>
  </div>
</div>
