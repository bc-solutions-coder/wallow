"""Keep historical source identity distinct from the Actions controller revision."""

from dataclasses import dataclass

from publication import Producer, PublicationError, matches, positive_integer


@dataclass(frozen=True)
class RecoveryProducer(Producer):
    controller_sha: str
    recovery_request_sha256: str
    release_id: int

    def __post_init__(self):
        if not matches(r'[0-9a-f]{40}', self.controller_sha) or not matches(r'[0-9a-f]{40}', self.source_sha) or not matches(r'sha256:[0-9a-f]{64}', self.recovery_request_sha256) or not positive_integer(self.release_id):
            raise PublicationError('Recovery producer lacks exact source and request identity')


def producer_from_record(record):
    if not isinstance(record, dict):
        raise PublicationError('Producer record must be an object')
    kind = RecoveryProducer if 'controller_sha' in record else Producer
    try:
        return kind(**record)
    except TypeError:
        raise PublicationError('Producer record has missing or unexpected identity fields') from None


def invocation_sha(producer):
    return producer.controller_sha if isinstance(producer, RecoveryProducer) else producer.source_sha


def payload_identity(producer, kind, variant):
    identity = {'schema': 1, 'repository': producer.repository, 'sha': invocation_sha(producer),
                'run_id': str(producer.run_id), 'run_attempt': str(producer.run_attempt),
                'workflow_ref': producer.workflow_ref, 'kind': kind, 'variant': variant}
    if isinstance(producer, RecoveryProducer):
        identity.update(schema=2, source_sha=producer.source_sha,
                        recovery_request_sha256=producer.recovery_request_sha256, release_id=producer.release_id)
    return identity
