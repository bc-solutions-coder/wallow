"""Bind a Pages preparation to the exact authorized DocFX producer artifact."""

from pathlib import Path
import tempfile

from publication import Producer, PublicationError
from publication_artifacts import Artifact, unpack_payload
from publication_site import prepare_site


def verify_site(client, plan, destination):
    selected = [item for item in plan['artifacts'] if item.get('kind') == 'docs']
    if len(selected) != 1 or selected[0].get('payload') != 'site.tar.gz' or selected[0].get('variant') != 'docfx':
        raise PublicationError('Authorized producer requires one exact DocFX site artifact')
    item = selected[0]
    artifact = Artifact(**{key: item[key] for key in ('id', 'name', 'digest', 'size')})
    producer = Producer(**plan['producer'])
    destination = Path(destination)
    if destination.exists() or destination.is_symlink():
        raise PublicationError('Site preparation directory must be new')
    with tempfile.TemporaryDirectory(prefix='wallow-site-inspection-') as directory:
        root = Path(directory)
        archive = client.download(artifact, root / 'site.zip')
        payload = unpack_payload(archive, root / 'verified', artifact, producer,
                                 'site.tar.gz', 'docs', 'docfx', 1024 * 1024 * 1024)
        destination.mkdir(parents=True)
        try:
            prepared = prepare_site(payload, destination / 'artifact.tar')
        except BaseException:
            destination.rmdir()
            raise
    return {'source_artifact': item, **prepared}
