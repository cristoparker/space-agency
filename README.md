# Space Mission Tycoon

A space agency management game made for the NASA International Space Apps Challenge 2026.

In Space Mission Tycoon, the player runs a space agency. They take contracts from clients, build a launch site, train staff, design rockets part by part and launch real-style missions to Earth orbit, the Moon and Mars. Every decision has a benefit, a cost and a risk, and the rocket flies according to the real rules of rocketry.

## Team

| Name | Role |
| --- | --- |
| Prima | Team Leader and Data Analyst |
| Sabit | Game Developer |
| Tasrif | Game Designer and Artist |
| Afnan | Editor and Storyteller |

## Challenge

**Challenge:** *[Add the name of the 2026 Space Apps challenge this project answers.]*

## The Problem

Spaceflight is hard to understand from the outside. People see a launch for a few minutes, but they do not see the planning behind it: why rockets have stages, why engines are tested before flight, why a Mars rover needs its own power, or why a mission can fail even when everything looks ready.

## Our Solution

Space Mission Tycoon turns these ideas into decisions the player makes. The player is the director of an agency, not a pilot. They choose what to build, what to test and how much risk to accept. The game then shows the result: the rocket reaches orbit, partly succeeds or fails, and the report explains why.

The missions follow the same path that real space agencies took: low Earth orbit first, then special orbits, then crew, then the Moon, then Mars.

## How the Game Works

1. **Take a contract.** A client offers a mission with a target, a payload, a budget, a mass limit and a deadline.
2. **Prepare the agency.** Build or upgrade facilities, train scientists and pilots, and test hardware.
3. **Design the rocket.** Stack engines, fuel tanks, stages and a payload bay that can reach the target within the mass limit.
4. **Build and deploy it.** Construction takes game days. The finished rocket moves to the launch pad with a launch date.
5. **Launch.** A countdown and a cinematic lift-off are followed by a flight that is simulated from the actual rocket design.
6. **Get the result.** Money, reputation and client trust change, and new contracts become available.

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

The player names the agency, chooses its type (Government, Private Company, Research Institute or Commercial Startup), shares 8 points between four skills (Engineering, Science, Finance and Safety) and designs a logo.

### The space centre

The launch site sits between a river and a mountain range. It has a launch complex with a 100 m service tower, a power grid, an office, staff housing and five empty plots. On these plots the player can build Mission Control, an Assembly Hangar, a Fuel Depot, a Tracking Station, a Research Lab and an Engine Test Stand. Each facility has three levels. Only five plots are available, so the player has to choose.

### The rocket builder

The player builds rockets from more than 50 parts in three sizes: engines, side boosters, fuel tanks, nose cones, payload fairings, crew capsules, satellites, a rover, a lander, solar panels, batteries, a nuclear power source, antennas, avionics, decouplers, parachutes, landing legs and thrusters. The builder shows the rocket's numbers live and warns about problems. A weak design is never blocked; it simply fails more often.

The design rules follow real rocketry:

- The engines must lift the full weight of the rocket.
- Sea-level engines work best in the lower atmosphere, and vacuum engines are more efficient in space.
- Stages drop empty tanks and spent engines to save mass.
- The payload must work at its destination: a rover needs power, and a Mars relay needs a strong antenna.
- Every part adds cost and mass, within the limits of the contract.

### Training and testing

Scientist training makes tests faster and raises reputation. Pilot training qualifies a crew for capsule flights. Engine, structural and communication tests reduce the cost of rockets or increase contract rewards, in the same way real agencies test hardware before flight.

### Random events

During each contract, about three mission events and one client request appear. Examples are a fault found in a test run, a sponsor offer, storm damage on the launch pad and a client who wants an extra objective. Each event offers two choices with a clear benefit, cost and risk, and the choice changes the chance of success on launch day.

### Launch day

On launch day the clock stops, a countdown runs and the rocket lifts off, filmed by three cameras. The flight is simulated from the player's design, using thrust, fuel, mass, air drag and gravity. The camera then moves to the agency's office, where a wall monitor shows the rocket's path around the Earth. The final report gives the result, the reason behind it, a star rating and the reward.

### Time and resources

The game clock starts from the real current time in Dhaka, Bangladesh, and the player can pause it or speed it up to 500 times. Day and night change over the site. Every building uses power from the grid, and money comes from contracts and sponsors.

## Use of NASA Data

We are connecting the game to open data from NASA so that real events on Earth and in space shape what happens in the game. The table below shows each data source and how we use it.

| Game feature | NASA data source | Use in the game |
| --- | --- | --- |
| Weather and solar power at the launch site | [NASA POWER](https://power.larc.nasa.gov/) | Real sunlight, temperature and wind at the site's location set the solar output of the power grid and can delay a launch. |
| Space weather events | [DONKI](https://api.nasa.gov/) (Space Weather Database of Notifications, Knowledge, Information) | Real solar flares and geomagnetic storms create events that raise the risk to satellites. |
| Natural hazard events | [EONET](https://eonet.gsfc.nasa.gov/) (Earth Observatory Natural Event Tracker) | Real cyclones, wildfires and floods create storm events at the launch site and shape Earth observation contracts. |
| Asteroid contracts | [NeoWs](https://api.nasa.gov/) (Near Earth Object Web Service) | Real asteroids passing close to Earth create new tracking contracts. |
| Mission control view | [JPL Horizons](https://ssd.jpl.nasa.gov/horizons/) | The real positions of the Moon and Mars on the in-game date are shown on the mission control monitor. |
| Deep space communication | [Deep Space Network Now](https://eyes.nasa.gov/dsn/dsn.html) | A live panel in the office shows which spacecraft NASA's antennas are talking to. |
| Earth observation results | [GIBS](https://www.earthdata.nasa.gov/engage/open-data-services-software/earthdata-developer-portal/gibs-api), [EPIC](https://epic.gsfc.nasa.gov/) and [FIRMS](https://firms.modaps.eosdis.nasa.gov/) | After a successful satellite mission, the player sees real images of the Earth. |

The game will keep a saved copy of each data feed, so it still works without an internet connection.

## Running the Project

1. Install Unity **6000.6.5f1** through Unity Hub.
2. Clone this repository and open the folder in Unity Hub.
3. Open the scene `Assets/Scenes/1. Main Menu.unity` and press Play.

The game has three scenes: the main menu, the space centre and the flight scene.

## Tools and Technologies

- **Unity 6** with the Universal Render Pipeline, the Input System and TextMeshPro
- **C#** for all game logic
- **Blender**, driven by Python scripts, for every 3D model, the part icons and the road textures
- **Python** for the generated music

All 3D models, textures and music were made by the team. The nature assets for the mountains come from the Holotna Mountain pack. The interface uses the Audiowide, Orbitron and Rajdhani fonts from Google Fonts.

## Use of AI

We used Claude Code (Anthropic) as a coding and writing assistant to help write and review code and documentation. All game design, missions, rules and artistic direction come from the team.

## References

- NASA Open APIs: https://api.nasa.gov
- NASA POWER: https://power.larc.nasa.gov
- NASA Earthdata: https://www.earthdata.nasa.gov
- JPL Solar System Dynamics: https://ssd.jpl.nasa.gov
- NASA Space Apps Challenge: https://www.spaceappschallenge.org
