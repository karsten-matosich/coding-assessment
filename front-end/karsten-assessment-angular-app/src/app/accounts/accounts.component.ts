import { Component, OnInit } from '@angular/core';
import { AccountsService, Account, CreateAccountRequest, UpdateAccountRequest } from '../services/accounts.service';
import { UtilsService } from '../services/utils.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-accounts',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './accounts.component.html',
  styleUrls: ['./accounts.component.css']
})
export class AccountsComponent implements OnInit {
  accounts: Account[] = [];
  showAccountForm: boolean = false;
  isEditMode: boolean = false;
  editingAccountId: number | null = null;
  newAccountName: string = '';
  newAccountNumber: string = '';

  constructor(
    private accountsService: AccountsService,
    private utilsService: UtilsService
  ) {}

  ngOnInit() {
    this.refreshAccounts();
  }

  formatCurrency(value: number): string {
    return this.utilsService.formatCurrency(value);
  }

  isNegative(value: number): boolean {
    return value < 0;
  }

  private closeAllForms() {
    this.showAccountForm = false;
    this.isEditMode = false;
    this.editingAccountId = null;
    this.newAccountName = '';
    this.newAccountNumber = '';
  }

  openAccountForm() {
    this.closeAllForms();
    this.showAccountForm = true;
  }

  editAccount(account: Account) {
    this.closeAllForms();
    this.isEditMode = true;
    this.editingAccountId = account.id;
    this.newAccountName = account.name;
    this.newAccountNumber = account.account_number;
    this.showAccountForm = true;
  }

  cancelAccount() {
    this.closeAllForms();
  }


  private refreshAccounts() {
    this.accountsService.getAccounts().subscribe({
      next: (data) => {
        this.accounts = data;
      },
      error: (error) => {
        console.error('Error fetching accounts:', error);
      }
    });
  }

  private handleServiceCall(
    serviceCall: () => Observable<any>,
    errorMessage: string,
    onSuccess?: () => void
  ) {
    serviceCall().subscribe({
      next: () => {
        this.refreshAccounts();
        if (onSuccess) onSuccess();
      },
      error: (error) => {
        let displayMessage = errorMessage;
        if (error?.error?.message) {
          displayMessage = error.error.message;
        } else if (error?.message) {
          displayMessage = error.message;
        }
        alert(displayMessage);
      }
    });
  }

  isFormValid(): boolean {
    return !!this.newAccountName.trim() && !!this.newAccountNumber.trim();
  }

  private createAccountRequest(): CreateAccountRequest | UpdateAccountRequest {
    return {
      Name: this.newAccountName,
      AccountNumber: this.newAccountNumber
    };
  }

  createAccount() {
    const request = this.createAccountRequest() as CreateAccountRequest;
    this.handleServiceCall(
      () => this.accountsService.createAccount(request),
      'Error creating account',
      () => this.cancelAccount()
    );
  }

  updateAccount() {
    if (this.editingAccountId === null) return;

    const request = this.createAccountRequest() as UpdateAccountRequest;
    this.handleServiceCall(
      () => this.accountsService.updateAccount(this.editingAccountId!, request),
      'Error updating account',
      () => this.cancelAccount()
    );
  }

  saveAccount() {
    if (this.isEditMode) {
      this.updateAccount();
    } else {
      this.createAccount();
    }
  }
}