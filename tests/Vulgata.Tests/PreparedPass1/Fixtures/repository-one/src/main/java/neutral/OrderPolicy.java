package neutral;

public final class OrderPolicy {
    public boolean maySubmit(int amount) {
        return amount > 0 && amount <= 1000;
    }
}
