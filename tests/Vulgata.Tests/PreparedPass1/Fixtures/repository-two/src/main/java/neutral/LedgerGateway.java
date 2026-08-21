package neutral;

public final class LedgerGateway {
    public String record(String orderId) {
        return "recorded:" + orderId;
    }
}
