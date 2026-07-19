from filtered_target import filtered_target
import isolated_module


class BaseProcessor:
    def process(self):
        return 1


class DerivedProcessor(BaseProcessor):
    def process(self):
        return filtered_target()


class IsolatedClass:
    pass


def helper():
    return 2


def isolated_function():
    return 3
