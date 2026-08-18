package example.analysis;

import java.util.Objects;

public final class GreetingService {
    private final GreetingFormatter formatter;

    public GreetingService(GreetingFormatter formatter) {
        this.formatter = Objects.requireNonNull(formatter);
    }

    public String createGreeting(String subject) {
        return formatter.format(subject);
    }
}
