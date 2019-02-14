Write-Host Installing the cert-manager in namespace cert-manager
kubectl create namespace cert-manager
kubectl label namespace cert-manager certmanager.k8s.io/disable-validation=true
kubectl apply -f https://raw.githubusercontent.com/jetstack/cert-manager/release-0.6/deploy/manifests/00-crds.yaml
kubectl apply -f https://raw.githubusercontent.com/jetstack/cert-manager/release-0.6/deploy/manifests/cert-manager.yaml --validate=false

Write-Host Creating the production cluster issuer
kubectl apply -f ./create-cluster-issuer.yaml

Write-Host All done!
Read-Host