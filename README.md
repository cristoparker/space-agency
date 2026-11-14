# Space Mission Tycoon

A space agency management game for one to four players, made for the NASA International Space Apps Challenge 2026.

## Videos

**Short video**

[![Space Mission Tycoon: short video](https://img.youtube.com/vi/iGuKzTSPkzY/hqdefault.jpg)](https://www.youtube.com/watch?v=iGuKzTSPkzY)

**Full video**

[![Space Mission Tycoon: full video](https://img.youtube.com/vi/4k0GA2XsnKI/hqdefault.jpg)](https://www.youtube.com/watch?v=4k0GA2XsnKI)

## Overview

In Space Mission Tycoon, the player runs a space agency. They take contracts from clients, build a launch site, train staff, design rockets part by part and launch real-style missions to Earth orbit, the Moon and Mars. Every decision has a benefit, a cost and a risk, and the rocket flies according to the real rules of rocketry.

The game can be played alone, or by a team of up to four friends over Steam. In team play, each player takes a role (Engineer, Scientist, Finance or Safety) and the team runs one agency together.

## Team

| Name | Role |
| --- | --- |
| Prima | Team Leader and Data Analyst |
| Sabit | Game Developer |
| Tasrif | Game Designer and Artist |
| Afnan | Editor and Storyteller |

## Challenge

**Challenge:** Space Mission Design Game (NASA Space Apps Challenge 2026)

The challenge asks for an interactive game in which players design, manage and simulate a complete space mission, make engineering decisions, manage limited resources and see how each choice shapes the result. It also suggests team play for up to four people with different responsibilities.

## The Problem

Spaceflight is hard to understand from the outside. People see a launch for a few minutes, but they do not see the planning behind it: why rockets have stages, why engines are tested before flight, why a Mars rover needs its own power, or why a mission can fail even when everything looks ready. Students rarely get to try a whole mission from concept to launch, so these trade-offs stay abstract.

## Our Solution

Space Mission Tycoon turns these ideas into decisions the player makes. The player is the director of an agency, not a pilot. They choose what to build, what to test and how much risk to accept. The game then shows the result: the rocket reaches orbit, partly succeeds or fails, and the report explains why.

The missions follow the same path that real space agencies took: low Earth orbit first, then special orbits, then crew, then the Moon, then Mars.

## How the Game Works

1. **Take a contract.** A client offers a mission with a target, a payload, a budget, a mass limit and a deadline.
2. **Prepare the agency.** Build or upgrade facilities, train scientists and pilots, and test hardware.
3. **Design the rocket.** Stack engines, fuel tanks, stages and a payload bay that can reach the target within the mass limit.
4. **Build and deploy it.** Construction takes game days. The finished rocket moves to the launch pad with a launch date.
5. **Launch.** A countdown and a cinematic lift-off are followed by a flight that is simulated from the actual rocket design.
6. **Get the result.** Money, reputation and client trust change, the mission is explained in a learning report, and new contracts become available.

## Missions

The game includes 12 missions, from a student satellite to a lander on Mars.

| Mission | Target | Payload | Budget (M) | Mass limit (kg) | Special requirements |
| --- | --- | --- | --- | --- | --- |
| University Cubesat Launch | Earth orbit | Satellite | 45 | 250 | None |
| Coastal Weather Satellite | Earth orbit | Satellite | 85 | 600 | Communication of at least 0.5 Mbps |
| Earth Climate Watch | Earth orbit | Satellite | 120 | 1,200 | Sun-synchronous orbit, communication of at least 1 Mbps |
| Polar Ice Survey | Earth orbit | Radar satellite | 140 | 900 | Polar orbit, communication of at least 1 Mbps, power of at least 300 W |
| Space Telescope Delivery | Earth orbit | Telescope | 220 | 1,800 | Power of at least 700 W, communication of at least 3 Mbps |
| Crew Capsule Test Flight | Earth orbit | Crew | 260 | 3,500 | Parachute splashdown |
| Lunar Orbiter Survey | Moon | Satellite | 210 | 1,100 | Communication of at least 2 Mbps, power of at least 400 W |
| Lunar Research Station | Moon | Lander | 300 | 1,800 | Soft landing |
| Lunar Rover Pathfinder | Moon | Rover | 340 | 1,600 | Soft landing |
| Deep Space Comms Relay | Mars | Relay satellite | 380 | 1,400 | Communication of at least 4 Mbps, power of at least 600 W |
| Mars Surface Explorer | Mars | Rover | 450 | 2,500 | Power of at least 500 W |
| Mars Lander Demonstration | Mars | Lander | 520 | 2,200 | Soft landing, power of at least 450 W |

## Main Features

### The agency

The player names the agency, chooses its type and designs a logo from layered shapes and colours.

| Agency type | Effect |
| --- | --- |
| Government | Large budget, slow growth |
| Private Company | Balanced start |
| Research Institute | Less money, faster reputation |
| Commercial Startup | Small budget, cheaper rockets |

The player also shares 8 points between four skills: **Engineering** (cheaper rockets), **Science** (faster reputation), **Finance** (more starting money) and **Safety** (fewer launch failures).

### The space centre

The launch site sits between a river and a mountain range. It has a launch complex with a 100 m service tower, a power grid, an office, staff housing, two car parks and five empty plots. Traffic moves on the roads, and lamps and building lights come on at night.

On the five plots the player can build six kinds of facility. Each has three levels, each level costs money and game days, and bigger levels use more power. No facility can be upgraded past the level of the power grid.

| Facility | Purpose | Cost for levels 1 / 2 / 3 (M) |
| --- | --- | --- |
| Mission Control | Follows every contract and flight | 180 / 300 / 700 |
| Assembly Hangar | Builds rockets and trains pilots | 150 / 350 / 900 |
| Fuel Depot | Stores propellant for bigger rockets | 90 / 220 / 600 |
| Tracking Station | Tracks rockets and satellites | 120 / 260 / 700 |
| Research Lab | Research and structural tests | 140 / 320 / 800 |
| Engine Test Stand | Fires engines before launch day | 110 / 260 / 650 |

### The rocket and payload builders

The player builds rockets from more than 50 parts in three sizes: engines, side boosters, fuel tanks, nose cones, payload fairings, crew capsules, satellites, a rover, a lander, solar panels, batteries, a nuclear power source, antennas, avionics, decouplers, size adapters, fins, parachutes, landing legs, grid fins and thrusters. Each part can be painted. The builder shows the rocket's numbers live (mass, thrust-to-weight, delta-v, stability, power and data rate) and warns about problems. A weak design is never blocked; it simply fails more often.

The same builder has a payload mode, where the player fits power and communication equipment around a base payload. A simple mode lets beginners pick one option per category, and the game builds the full stack for them. Designs can be saved and reused, and ready-made blueprints can be bought.

The design rules follow real rocketry:

- The engines must lift the full weight of the rocket.
- Sea-level engines work best in the lower atmosphere, and vacuum engines are more efficient in space.
- Stages drop empty tanks and spent engines to save mass.
- The payload must work at its destination: a rover needs power, and a Mars relay needs a strong antenna.
- Every part adds cost and mass, within the limits of the contract.

### Training and testing

| Programme | Where | Effect |
| --- | --- | --- |
| Scientist Training I, II, III | Office | Tests finish faster, reputation grows faster |
| Pilot Training I, II, III | Assembly Hangar | Qualifies a crew for capsule flights |
| Engine Static-Fire Test | Engine Test Stand | Rockets cost 4% less to build |
| Structural Load Test | Research Lab | Rockets cost 3% less to build |
| Comms Link Test | Tracking Station | Contracts pay 10% more |

Each programme has a chance of success shown before it starts. A failed programme costs its money and days, but makes the next attempt safer. Training fades after a few months, and tests must be repeated for each new contract.

### Contracts and the global market

The Contracts window has three tabs: **Offers** (accept or decline), **Global** (an auction in which agencies bid down the price of contracts posted from around the world) and **Active** (deadline bars, pending contracts and abandon). One contract runs at a time; the others wait as pending. Client trust carries over between contracts and changes how well future contracts pay.

### Random events

During each contract, about three mission events and one client call appear. Examples are a fault found in a test run, a sponsor offer, a lighter bracket, a veteran's advice, storm damage on the launch pad and a client who wants an extra objective. Each event offers two choices with a clear benefit, cost and risk, and the choice changes the chance of success on launch day.

### Launch day

On launch day the clock stops, a countdown runs and the rocket lifts off, filmed by three cameras. The flight is simulated from the player's design, using thrust, fuel, mass, air drag and gravity, with a pitch programme that bends the path towards orbit. The camera then moves to the agency's office, where a wall monitor shows the rocket's path around the Earth. The player can skip the cutscene at any moment without losing any information.

The result is a success, a partial success or a failure. A failure has a real-world cause: a missing vital part, engines too weak to lift off, an engine failure, a loss of control, or a structural failure at maximum dynamic pressure.

### Feedback and learning

- **Impact cards:** before every big decision, a card shows its benefits, costs and risks.
- **Objectives menu:** each running contract is listed as steps (training, testing, building, deploying, launching) with ticks and progress bars.
- **Mission in flight:** after launch, a panel follows the flight, its rating and the reward waiting at the end.
- **Launch report and final result:** a report right after launch, and a final result with stars and reward.
- **Learning outcome:** every launch is written into a mission diary (delta-v against the target, thrust-to-weight, stability, reliability, training and tests done, cost against budget, risky choices). A learning screen turns it into a plain-language explanation of why the mission went the way it did.

### Time and resources

The game clock starts from the real current time in Dhaka, Bangladesh. The player can pause it or speed it up to 500 times. Day and night change over the site, and a calendar drops down from the date in the top bar. Every building uses power from the grid; every game hour a small popup shows the energy used and the money spent, and a warning appears when power runs short. Money comes from contracts and sponsors.

## Team Play (Multiplayer)

Up to **four players** run one agency together over Steam. This answers the challenge's call for teams of up to four with different responsibilities.

### Getting together

The **Team Play** window opens from the main menu, or from the Team button at the top right during the game. It has three tabs:

- **Friends:** the player's Steam friends with avatars and status, sorted with players of this game first. Players can host a shared agency, invite friends and join friends who are hosting. Joining also works from a Steam invite or a friend's Steam profile.
- **Agency:** the lobby. It shows who is in, who is the host, each player's role and who is ready. The host starts the game when everyone is ready.
- **Chat:** team chat. Joins, role changes, decisions and launches also appear in it. During the game, new messages appear briefly under the Team button, and Enter opens the chat.

### Roles

Each player can take one free role. A role that nobody holds can be done by anyone, so a team of two still has full control of the agency.

| Role | Responsibilities | Real-world counterpart |
| --- | --- | --- |
| Engineer | Designs, builds and deploys rockets, and picks the launch date | Spacecraft and launch vehicle engineering |
| Scientist | Runs training and tests | Science and test teams |
| Finance | Accepts and declines contracts, and builds and upgrades facilities | Programme management and budget |
| Safety | Chooses the answer to every random event and gives the GO for launch | Mission assurance and range safety |

If a player tries something that belongs to another role, the game tells them who decides. The roles depend on each other: the Engineer needs money from Finance, Finance needs the Scientist's tests to raise contract pay, and Safety decides how much risk the team takes to the launch pad.

### How a shared game works

1. The host's agency becomes the team's agency. Everyone loads it when the host starts. A guest's own single-player save is kept aside and returned when they leave.
2. The host's game is the only one that simulates. Every second it sends the changes (money, buildings, contracts, rocket, flight) to the other players. The clock follows the host, and pause or speed requests go to the host.
3. Every action a player takes is checked against their role and carried out by the host.
4. When a random event appears, the card opens for the whole team and the game pauses. Only the Safety officer can choose, and the choice is announced in chat.
5. On launch day the Safety officer gives the GO, and everyone watches the same countdown and flight with the same result.
6. A friend can join a game that has already started and goes straight into it.

### Technology

The multiplayer system is built with **PurrNet** (networking), **Steamworks.NET** (friends, lobbies and invites) and Steam's peer-to-peer connection, so no server needs to be rented. **ParrelSync** lets the team test with two copies of the project on one computer.

## Use of NASA Data

We are connecting the game to open data from NASA so that real events on Earth and in space shape the missions. The table shows which dataset feeds which part of the game.

| Contract or system | NASA data | Use in the game |
| --- | --- | --- |
| Every launch | [NASA POWER](https://power.larc.nasa.gov/) | Real wind, temperature and sunlight at the site: bad weather can delay a launch, and sunlight sets the solar output of the power grid. |
| Every contract | [DONKI](https://api.nasa.gov/) (space weather) | Real solar flares, coronal mass ejections and geomagnetic storms become events with extra risk to satellites and crew. |
| Coastal Weather Satellite | [EONET](https://eonet.gsfc.nasa.gov/) and [GIBS](https://www.earthdata.nasa.gov/engage/open-data-services-software/earthdata-developer-portal/gibs-api) | A real cyclone in the Bay of Bengal raises the contract's urgency; real imagery of the storm is the satellite's first picture. |
| Earth Climate Watch | GIBS climate layers | The satellite returns today's real global temperature map. |
| Polar Ice Survey | GIBS sea ice concentration imagery | A real polar ice map for the mission date, and a lesson on why only a polar orbit sees both poles. |
| University Cubesat Launch | [NASA Image and Video Library](https://images.nasa.gov/) | Real cubesat photos in the mission briefing. |
| Space Telescope Delivery | [NASA Exoplanet Archive](https://exoplanetarchive.ipac.caltech.edu/) | The telescope's discoveries are real confirmed exoplanets. |
| Crew Capsule Test Flight | DONKI radiation events, NASA Image and Video Library | A real solar particle event is a reason for Safety to hold the launch. |
| Lunar missions | [JPL Horizons](https://ssd.jpl.nasa.gov/horizons/) and Moon Trek | The Moon appears where it really is on the game date; real lunar landing-site maps. |
| Mars missions | JPL Horizons and [Mars Trek](https://api.nasa.gov/mars-wmts/catalog) | Real Mars launch windows and real landing-site imagery. |
| Deep Space Comms Relay | [Deep Space Network Now](https://eyes.nasa.gov/dsn/dsn.html) and JPL Horizons | A live office panel shows which spacecraft NASA's antennas are talking to; data rate falls with distance. |
| Asteroid contracts | [NeoWs](https://api.nasa.gov/) and the [JPL close-approach API](https://ssd-api.jpl.nasa.gov/doc/cad.html) | New tracking contracts from real asteroids passing close to Earth. |

The game will keep a saved copy of each data feed, so it still works without an internet connection, and it will name each dataset on screen where it is used.

## Coverage of the Challenge

| Area | Status |
| --- | --- |
| Complete mission from contract to launch and result | Covered |
| A consequence for every decision (benefit, cost, risk) | Covered |
| Team play for up to four players with different roles | Covered |
| Spacecraft components: structure, power, batteries, communications, computer, attitude control | Covered |
| Launch vehicle capacity and mass checks | Covered (players build their own launch vehicle) |
| Resources: mass, power, budget, communications, time | Covered |
| Random events, mission scenarios, reports and post-mission analysis | Covered |
| Spacecraft visualisation and live resource display | Covered |
| Scientific instruments with data and science value | Planned |
| Choice of orbit and its effects | Planned |
| Mission operations after launch | Partly |
| Difficulty levels, leaderboard, sandbox mode | Planned |

## Running the Project

1. Install Unity **6000.6.5f1** through Unity Hub.
2. Clone this repository and open the folder in Unity Hub. Unity downloads the packages listed in `Packages/manifest.json`, including PurrNet, Steamworks.NET and ParrelSync.
3. Open the scene `Assets/Scenes/1. Main Menu.unity` and press Play.

The game has three scenes: the main menu, the space centre and the flight scene.

### Testing team play

- **Over Steam:** start the Steam client and sign in. The project uses Steam's test App ID 480 (`steam_appid.txt`), so Steam shows players as playing "Spacewar". Register an agency with New Game, open Team Play, press "Host with Steam" and invite a friend.
- **On one computer:** create a clone with ParrelSync (menu ParrelSync / Clones Manager) and open it. In the main editor press "Host local test" in Team Play, and in the clone press "Join local test".
- If the network prefab is missing, run **Tools / Space Agency / Setup Multiplayer** in the editor.

## Tools and Technologies

- **Unity 6** with the Universal Render Pipeline, the Input System and TextMeshPro
- **C#** for all game logic
- **PurrNet**, **Steamworks.NET** and **ParrelSync** for team play
- **Blender**, driven by Python scripts, for every 3D model, the part icons and the road textures
- **Python** for the generated music

All 3D models, textures and music were made by the team. The nature assets for the mountains come from the Holotna Mountain pack. The interface uses the Audiowide, Orbitron and Rajdhani fonts from Google Fonts.

## Use of AI

We used Claude Code (Anthropic) as a coding and writing assistant to help write and review code and documentation. The learning outcome screen uses fixed, real rocketry rules to explain each mission; it does not call an online AI model. All game design, missions, rules and artistic direction come from the team.

## References

- NASA Space Apps Challenge 2026, "Space Mission Design Game": https://www.spaceappschallenge.org/2026/challenges/space-mission-design-game/
- NASA Open APIs: https://api.nasa.gov
- NASA POWER: https://power.larc.nasa.gov
- NASA Earthdata and GIBS: https://www.earthdata.nasa.gov
- JPL Solar System Dynamics and Horizons: https://ssd.jpl.nasa.gov
- JPL SSD APIs (close approaches): https://ssd-api.jpl.nasa.gov/doc/
- NASA Exoplanet Archive: https://exoplanetarchive.ipac.caltech.edu
- NASA Image and Video Library: https://images.nasa.gov
- PurrNet: https://purrnet.dev
- Steamworks.NET: https://github.com/rlabrecque/Steamworks.NET
- ParrelSync: https://github.com/VeriorPies/ParrelSync
