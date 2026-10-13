package api1.make_transfer_higher_limit_v2

default allow := false

service := data.authorization.services.api1

# Each assigned task supplies a flat grant list, as in RequestPermissions.
granted_tasks contains task if {
    some role in input.roles
    some assigned_task in service.roles[role]
    some task in service.tasks[assigned_task]
}

# Evaluate the transfer using only grants resolved from authenticated roles.
valid_transfer if {
    "api1:make-transfer" in granted_tasks
    input.resource.amount > 0
}

allow if {
    valid_transfer
    input.resource.amount <= service.standardLimit
}

allow if {
    valid_transfer
    "api1:make-transfer-higher-limit" in granted_tasks
}
