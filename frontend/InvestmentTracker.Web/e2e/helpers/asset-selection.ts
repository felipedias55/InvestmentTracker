import { expect, Locator } from '@playwright/test';

export async function selectAsset(scope: Locator, ticker?: string) {
  const selector = scope.locator('app-asset-selector');
  await selector.getByRole('button', { name: /^(Selecionar|Trocar) ativo$/ }).click();
  if (ticker) {
    await selector.getByRole('searchbox', { name: 'Buscar ativo por código ou nome', exact: true }).fill(ticker);
    await expect(selector.locator('.choice')).toHaveCount(1);
  }
  await selector.locator('.choice').first().click();
}
