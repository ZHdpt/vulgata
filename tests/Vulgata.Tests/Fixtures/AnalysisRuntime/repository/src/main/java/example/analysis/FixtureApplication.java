package example.analysis;

public final class FixtureApplication {
    private FixtureApplication() {
    }

    public static String run(String subject) {
        GreetingFormatter formatter = new DefaultGreetingFormatter();
        GreetingService service = new GreetingService(formatter);
        return service.createGreeting(subject);
    }
}
