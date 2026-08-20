package neutral.app;

public final class OrderGateway {
    public String submit(String orderId) {
        return "accepted:" + orderId;
    }
}
