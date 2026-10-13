package api1.make_transfer_higher_limit_v1

default allow := false

# The application supplies effective task grants and the transfer facts.
valid_transfer if {
    "api1:make-transfer" in input.allowedTasks
    input.resource.amount > 0
}

allow if {
    valid_transfer
    input.resource.amount <= data.authorization.services.api1.standardLimit
}

allow if {
    valid_transfer
    "api1:make-transfer-higher-limit" in input.allowedTasks
}
