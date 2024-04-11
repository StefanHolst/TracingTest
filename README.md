# Tracing Test
This is just a test of distributes tracing with a tail sampling.

## Contains
- NATS Server
- Grafana
- Tempo
- OpenTelemetry Collector
- Console Application: Connects to nats to generate random numbers. But it fails randomly.
- Web Application: A weather app that send NATS requests to the console app for random numbers to use in weather predictions


## Running
- Run the `Everything` run configuration in rider.
- Go to `http://localhost:5000/weatherforecast`. This will generate a weather forecast, but sometimes it will fail.
- Open Grafana `localhost:3000` and go to the explore page. Notice only the failed request are saved.
