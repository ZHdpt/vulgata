package example.analysis;

public final class DefaultGreetingFormatter implements GreetingFormatter {
    @Override
    public String format(String subject) {
        return "Hello, " + subject + ".";
    }
}
