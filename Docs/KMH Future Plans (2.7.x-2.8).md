# KMH 2.7.x to 2.8 Roadmap / Current Thoughts

**2.7 gave KMH the foundation. 2.7.x is where I want to clean it up, optimize it, and make it stronger. 2.8 is where I want everything to feel alive.**

Right now KMH is on **2.7**, and honestly, 2.7 became a lot bigger than I first expected.

My original plan for 2.7 was mainly focused on the site system. I wanted to clean up how sites worked, make workers easier to understand, improve custom site menus, fix confusing assign/join/leave logic, and make the whole thing feel more like a real KMH feature instead of something still stuck between old and new systems.

A lot of that did happen, but 2.7 also ended up pulling in way more than just site cleanup. It brought in guilds, treasuries, marketplace systems, WTB listings, quest board improvements, Discord support, leaderboards, custom sites, worker XP, UI improvements, server hardening, performance fixes, and a lot more.

Because of that, my original idea for 2.8 has shifted a bit.

At first, 2.8 was going to be the update where I introduced the real economy and trade systems. But now, a good amount of that already exists in 2.7. So 2.8 is not really about “adding the economy” anymore.

Now, I see 2.8 as the update that takes what 2.7 built and makes it feel more alive, more connected, safer, and more useful for the players.

## Where Things Are Right Now

For now, I am not trying to jump straight into 2.8.

The plan is to move through **2.7.x updates** first.

These updates are basically the bridge between 2.7 and 2.8. I want to use them to fix rough areas, improve performance, clean up the client side, test systems properly, and slowly build toward the bigger 2.8 direction.

I do not want 2.8 to be one massive update where everything gets thrown in at once and then we just hope it works. I would rather build toward it slowly, release improvements over time, and make sure the foundation is actually stable before stacking even more on top of it.

The way I see it right now:

**2.7 gave KMH the economy tools.**
**2.7.x makes those tools cleaner, faster, safer, and easier to use.**
**2.8 brings it all together into a living economy.**

## First Focus For 2.7.x

For the first couple of 2.7.x updates, especially the upcoming 2.7.2 / 2.7.3 area, I want the focus to be more on fixes and optimization instead of rushing into brand-new large systems.

Right now, RWT load times have gone up a little. This seems to apply to original RWT too, not just KMH, but since I have ported a lot of systems over and added a lot on top of it, there could be some deeper issues showing up now.

So before I push too hard into the bigger 2.8 economy features, I want to spend some time looking at the client side and the overall codebase.

The early focus will be things like:

* Client-side fixes
* Load time improvements
* General code cleanup
* Startup and loading behavior
* Reducing unnecessary allocations
* Cleaning up hot paths
* Looking into UI and client issues
* Checking if ported systems are slowing anything down
* Making sure KMH additions are not adding extra load problems
* Improving stability before adding more major features

This does not mean the 2.8 plans are being dropped. It just means I want to fix and optimize first.

I would rather have KMH feel smoother and more stable before adding even more systems. If load times are getting worse in both official RWT and KMH, then it is worth actually looking into instead of assuming it is only one thing.

It could be upstream changes, Discord/Rich Presence setup, config checks, UI loading, mod validation, startup logic, ported systems, or just a bunch of smaller issues stacking up over time.

So the early 2.7.x direction is basically:

**Fix first. Optimize first. Make the client feel better first. Then keep building toward 2.8.**

## What I Want 2.7.x To Be

2.7.x is the cleanup and build-up phase.

2.7 added a lot, and when an update gets that big, there are always going to be rough edges. Some systems need better wording, some menus need better layout, some actions need clearer feedback, and some features just need more testing with real players using them.

For 2.7.x, I want to focus on:

* Making sites easier to understand and manage
* Cleaning up worker assignment and site production
* Improving marketplace clarity and refresh behavior
* Making WTB listings and trade actions feel smoother
* Improving Discord economy messages and notifications
* Expanding the quest board with more meaningful player-made quests
* Testing early versions of more physical trade systems
* Improving safety checks around files, configs, and server actions
* Strengthening validation for economy, quest, and treasury systems
* Fixing bugs and edge cases as players report them
* Optimizing code and reducing slowdown where possible

I do not want 2.7.x to be flashy just for the sake of it. Some of these updates might not sound as exciting as a huge new system, but they matter a lot.

A system that is stable, fast, and understandable is way better than a giant feature that barely works or confuses everyone.

## Sites Still Matter

Sites were one of the original major focuses for 2.7, and I still want to keep improving them.

I want sites to feel more natural for players. Players should be able to understand who is assigned, who is working, how production works, how upgrades work, and what each button actually does.

The goal is to keep moving sites away from that “old logic mixed with new logic” feeling.

Sites should feel like a polished KMH system, not a side feature that players have to guess their way through.

So 2.7.x will still include site cleanup where needed, especially around clarity, worker behavior, custom site syncing, refresh behavior, upgrade flow, and better server responses when something fails.

## Marketplace Direction

The marketplace already exists now, so I do not need to rebuild it from scratch.

The next step is making it feel better.

I want players to have clearer listings, better search and filtering, smoother buying and selling, better WTB matching, and better feedback when something sells, expires, gets canceled, or updates.

The marketplace should feel like a real part of the server, not just a menu people check once and forget about.

I also want Discord to support marketplace activity better. Not in a way that replaces the in-game systems, but in a way that keeps players connected when they are not online.

Eventually I want things like:

* Sale notifications
* WTB match notifications
* Quest updates
* Trade status updates
* Economy event notices
* Cleaner Discord formatting for market activity

That kind of stuff helps make the server feel active even when people are offline.

## Quest Board Direction

The quest board is already in place, but I want it to become more than basic item requests.

One of the bigger ideas I want to explore is letting players create more interesting jobs for each other.

Things like:

* Defend my colony from a raid
* Escort something or someone across the map
* Hunt a specific target
* Build something at a location
* Complete a custom task and submit proof

That would make the quest board feel more like an actual player job board instead of just another economy menu.

I also want quest history and reputation to matter eventually. If someone completes quests often and does them properly, players should be able to see that they are reliable. If someone keeps abandoning jobs or abusing the system, that should matter too.

Not as a permanent punishment, but as a trust system.

People should be able to earn trust back, but the server should still give players a way to judge who they are trading with or accepting quests from.

## Trading Direction

One of the biggest things I want to move toward is making trade feel more physical.

Right now, a lot of trading is menu-based. That works for now, but long-term I want it to feel more like RimWorld.

If someone buys something from another player, I do not want it to always feel like the item just magically appeared.

I want to slowly test systems where trading feels more connected to the world itself.

That could mean things like:

* Buying through existing in-game systems
* Comm console marketplace access
* Orbital-style trading options
* Drop pod delivery
* Delivery delays
* Delivery fees
* Distance-based trade logic
* Caravan delivery ideas later on

I am not trying to rush all of this at once, but this is the kind of direction that will make 2.8 feel different.

The goal is to make the economy feel like it actually moves through the world.

## Safety, Security, and Trust

Another thing I want to build more into 2.7.x and 2.8 is safety and trust.

As KMH grows, it touches more systems. Mods, configs, server files, self-hosting, Discord commands, economy transactions, treasuries, quests, player data, and more.

Because of that, I want to be more careful with how things are handled.

I do not want safety to feel annoying or overcomplicated, but I do want KMH to be harder to abuse and easier to trust.

That means improving things like:

* Safer file and config handling
* Better validation before the server accepts actions
* Better protection against bad packets or broken data
* Stronger checks around marketplace and treasury actions
* Better logging when something suspicious or invalid happens
* Safer defaults for players and server owners
* More reliable recovery when something goes wrong
* Better permission checks for server and Discord actions

If players are trading items, storing silver, using treasuries, posting quests, or hosting servers, those systems need to feel safe and reliable.

A failed action should not break things.
A bad request should not crash the server.
A trade should not duplicate or delete items unfairly.
A player should know what they are downloading and using.

That kind of trust matters more and more as the project grows.

2.8 is supposed to make the world feel alive, but that only works if the systems behind it are secure and reliable.

## What I Want 2.8 To Become

KMH 2.8 is where I want the economy to fully come together.

The marketplace, guilds, treasuries, quests, Discord bridge, sites, and leaderboards are already here now. 2.8 should be the update that connects those systems better and makes the server feel like it has momentum.

I want players to feel like their colony is part of a bigger world.

A player should be able to list an item, log off, and come back to see that something happened. Someone should be able to post a quest asking for help. Players should be able to build reputation, react to economy events, trade in ways that feel more connected to the map, and feel like the server keeps moving even when they are offline.

That is the main idea.

Not just more menus.
Not just more buttons.
An economy that actually moves.

## What I Am Not Trying To Do

There are also some things I do not want 2.8 to become.

I do not want fake systems added just to make the marketplace look busy. I would rather the economy be based on real player activity as much as possible.

I do not want real-money trading hooks. KMH is not touching that.

I do not want reputation to permanently ruin someone. Bad behavior should hurt trust, but people should be able to earn that trust back.

I also do not want to rush huge cross-server systems before the core economy is stable. Those ideas can be cool, but with different modlists, different planets, different server setups, and all the chaos that comes with that, I do not want to force it too early.

The priority right now is making the current systems better first.

## Final Thoughts

So for anyone following the project, this is basically where my head is at right now.

**2.7 was the foundation update.**
It brought in the core economy, guilds, sites, quests, treasuries, Discord tools, UI cleanup, and a lot of stability work.

**2.7.x is the build-up phase.**
This is where I want to fix rough edges, improve the client, optimize load times, clean up systems, improve safety, expand quests, polish the marketplace, and slowly test the systems that lead into 2.8.

**2.8 will be the Living Economy update.**
That is where I want KMH to feel more active, connected, secure, and player-driven.

The goal is not to rush one giant update. The goal is to slowly build this into something stable, trusted, useful, and actually fun for the people playing on the server.

I would rather take the time now to fix things properly, optimize what needs to be optimized, and make sure the foundation is healthy before stacking even more on top of it.

2.7 made the economy real.
2.7.x makes it cleaner and stronger.
2.8 is where I want it to feel alive.